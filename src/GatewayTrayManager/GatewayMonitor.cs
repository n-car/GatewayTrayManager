using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ServiceManager;

namespace GatewayTrayManager;

/// <summary>
/// Gateway-specific monitor that extends ServiceMonitor with HTTP health checks.
/// </summary>
public sealed class GatewayMonitor : ServiceMonitor
{
    private readonly Uri _gatewayBase;
    private readonly HttpClient _http;

    public GatewayMonitor(string serviceName, string gatewayBaseUrl, int timeoutSeconds, string? username = null, string? password = null)
        : base(serviceName)
    {
        _gatewayBase = new Uri(gatewayBaseUrl.TrimEnd('/') + "/");
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 30))
        };

        // Add Basic Auth header if credentials are provided
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    public async Task<GatewaySnapshot> GetGatewaySnapshotAsync()
    {
        var svcStatus = GetServiceStatusSafe(ServiceName);
        var (gwOk, gwInfo) = await CheckGatewayAsync();

        return new GatewaySnapshot(svcStatus, gwOk, gwInfo);
    }

    public override async Task<ServiceSnapshot> GetSnapshotAsync()
    {
        var snapshot = await GetGatewaySnapshotAsync();
        return new ServiceSnapshot(snapshot.ServiceStatus, snapshot.GatewayOk, snapshot.GatewayInfo);
    }

    public Task StartAsync() => StartServiceAsync();
    public Task StopAsync() => StopServiceAsync();
    public Task RestartAsync() => RestartServiceAsync();

    private async Task<(bool ok, string info)> CheckGatewayAsync()
    {
        try
        {
            // Try StatusPing first - fast health check
            var pingUrl = new Uri(_gatewayBase, "StatusPing");
            using var pingReq = new HttpRequestMessage(HttpMethod.Get, pingUrl);
            using var pingResp = await _http.SendAsync(pingReq);

            var status = (int)pingResp.StatusCode;
            
            // Handle 503 (Service Unavailable) - Gateway is starting up
            if (status == 503)
            {
                return (false, "503 Gateway Starting");
            }
            
            // Handle 404 (Not Found) - Endpoint doesn't exist, try alternative
            if (status == 404)
            {
                return await TryAlternativeEndpointAsync();
            }
            
            if (status < 200 || status >= 400)
            {
                return (false, $"{status} {pingResp.ReasonPhrase}");
            }

            // Parse StatusPing JSON to verify state is RUNNING
            var gatewayState = "UNKNOWN";
            try
            {
                var pingContent = await pingResp.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(pingContent))
                {
                    using var doc = JsonDocument.Parse(pingContent);
                    if (doc.RootElement.TryGetProperty("state", out var stateElement))
                    {
                        gatewayState = stateElement.GetString() ?? "UNKNOWN";
                    }
                }
            }
            catch
            {
                // Failed to parse StatusPing, continue anyway
            }

            var ok = gatewayState == "RUNNING";
            if (!ok)
            {
                return (false, $"State: {gatewayState}");
            }

            // If StatusPing is OK, try to get gateway info for more details
            try
            {
                var infoUrl = new Uri(_gatewayBase, "system/gwinfo");
                using var infoReq = new HttpRequestMessage(HttpMethod.Get, infoUrl);
                using var infoResp = await _http.SendAsync(infoReq, HttpCompletionOption.ResponseHeadersRead);

                if (infoResp.IsSuccessStatusCode)
                {
                    var content = await infoResp.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var info = ParseGatewayInfo(content);
                        if (!string.IsNullOrEmpty(info))
                        {
                            return (true, info);
                        }
                    }
                }
            }
            catch
            {
                // gwinfo failed, but StatusPing worked, so gateway is OK
            }

            return (true, "RUNNING");
        }
        catch (Exception ex)
        {
            return (false, ex.GetType().Name);
        }
    }

    private async Task<(bool ok, string info)> TryAlternativeEndpointAsync()
    {
        try
        {
            // If StatusPing doesn't exist (404), try just the root endpoint
            var rootUrl = new Uri(_gatewayBase, "");
            using var rootReq = new HttpRequestMessage(HttpMethod.Get, rootUrl);
            using var rootResp = await _http.SendAsync(rootReq);

            var status = (int)rootResp.StatusCode;
            
            if (status >= 200 && status < 400)
            {
                // Root endpoint responded, try gwinfo for details
                try
                {
                    var infoUrl = new Uri(_gatewayBase, "system/gwinfo");
                    using var infoReq = new HttpRequestMessage(HttpMethod.Get, infoUrl);
                    using var infoResp = await _http.SendAsync(infoReq);

                    if (infoResp.IsSuccessStatusCode)
                    {
                        var content = await infoResp.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            var info = ParseGatewayInfo(content);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return (true, info);
                            }
                        }
                    }
                }
                catch
                {
                    // gwinfo failed
                }

                return (true, $"{status} OK");
            }

            return (false, $"404 StatusPing N/A");
        }
        catch
        {
            return (false, "404 StatusPing N/A");
        }
    }

    private static string ParseGatewayInfo(string content)
    {
        try
        {
            // gwinfo returns key=value pairs separated by semicolons
            // Example: ContextStatus=RUNNING;PlatformName=Gateway-server;Version=8.3.3;PlatformEdition=;...
            
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var pairs = content.Split(';', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var pair in pairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    dict[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // Extract useful information
            var version = dict.TryGetValue("Version", out var v) ? v : null;
            var platformEdition = dict.TryGetValue("PlatformEdition", out var p) && !string.IsNullOrWhiteSpace(p) ? p : null;
            var contextStatus = dict.TryGetValue("ContextStatus", out var cs) ? cs : null;
            var redundancyStatus = dict.TryGetValue("RedundancyStatus", out var rs) ? rs : null;

            // Build a compact info string
            if (!string.IsNullOrEmpty(version))
            {
                var info = $"v{version}";
                
                if (!string.IsNullOrEmpty(platformEdition))
                {
                    info += $" {platformEdition}";
                }
                
                // Show redundancy status if not Independent
                if (!string.IsNullOrEmpty(redundancyStatus) && redundancyStatus != "Independent")
                {
                    info += $" ({redundancyStatus})";
                }
                
                return info;
            }

            // Fallback to just showing context status
            if (!string.IsNullOrEmpty(contextStatus))
            {
                return contextStatus;
            }

            return "OK";
        }
        catch
        {
            return "OK";
        }
    }

    public override void Dispose()
    {
        _http.Dispose();
        base.Dispose();
    }
}

public sealed record GatewaySnapshot(
    ServiceControllerStatus ServiceStatus,
    bool GatewayOk,
    string GatewayInfo)
{
    public bool CanStart => ServiceStatus is ServiceControllerStatus.Stopped;
    public bool CanStop => ServiceStatus is ServiceControllerStatus.Running;
    public bool CanRestart => ServiceStatus is ServiceControllerStatus.Running;
}
