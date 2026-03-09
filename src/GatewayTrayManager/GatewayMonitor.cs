using System;
using System.Collections.Generic;
using System.Net;
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
    private readonly HttpClientHandler? _handler;
    private readonly string? _username;
    private readonly string? _password;
    private readonly bool _useSessionAuth;
    private bool _isAuthenticated;

    public GatewayMonitor(string serviceName, string gatewayBaseUrl, int timeoutSeconds, 
        string? username = null, string? password = null, bool useSessionAuth = false)
        : base(serviceName)
    {
        _gatewayBase = new Uri(gatewayBaseUrl.TrimEnd('/') + "/");
        _username = username;
        _password = password;
        _useSessionAuth = useSessionAuth;

        // If using session auth, we need a handler with cookie container
        if (useSessionAuth && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            _handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true
            };
            _http = new HttpClient(_handler)
            {
                Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 30))
            };
        }
        else
        {
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 30))
            };

            // Add Basic Auth header if credentials are provided (non-session mode)
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
        }
    }

    /// <summary>
    /// Performs form-based login to obtain session cookie.
    /// Tries multiple known login endpoints for compatibility with different Ignition versions.
    /// </summary>
    public async Task<bool> LoginAsync()
    {
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
            return false;

        // List of possible login endpoints for different Ignition versions
        string[] loginEndpoints = 
        {
            "data/app/login",      // Standard web app login
            "web/login",           // Alternative web login
            "data/login",          // Data API login
            "login"                // Generic login
        };

        foreach (var endpoint in loginEndpoints)
        {
            try
            {
                var loginUrl = new Uri(_gatewayBase, endpoint);
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", _username),
                    new KeyValuePair<string, string>("password", _password)
                });

                using var response = await _http.PostAsync(loginUrl, content);

                // Success if 200, 302 (redirect after login), or 303 (see other)
                if (response.IsSuccessStatusCode || 
                    response.StatusCode == HttpStatusCode.Found || 
                    response.StatusCode == HttpStatusCode.SeeOther)
                {
                    _isAuthenticated = true;
                    return true;
                }

                // If we get 401/403, credentials might be wrong but endpoint exists
                if (response.StatusCode == HttpStatusCode.Unauthorized || 
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _isAuthenticated = false;
                    return false; // Credentials wrong, don't try other endpoints
                }

                // 404 means this endpoint doesn't exist, try next
            }
            catch
            {
                // Connection error, try next endpoint
            }
        }

        _isAuthenticated = false;
        return false;
    }

    /// <summary>
    /// Gets detailed gateway performance info from the authenticated API endpoint.
    /// Returns null if session auth is not enabled or authentication fails.
    /// Endpoint: /data/api/v1/systemPerformance/currentGauges
    /// Response: {"cpu":24.66,"heapMemory":1195661312,"maxMemory":2147483648}
    /// </summary>
    public async Task<GatewayApiInfo?> GetGatewayApiInfoAsync()
    {
        if (!_useSessionAuth || string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
            return null;

        try
        {
            // First try: use existing session
            var url = new Uri(_gatewayBase, "data/api/v1/systemPerformance/currentGauges");

            // If not authenticated yet, try to login
            if (!_isAuthenticated)
            {
                // Try form login first
                if (!await LoginAsync())
                {
                    // Form login failed, try Basic Auth directly on the API
                    return await GetGatewayApiInfoWithBasicAuthAsync();
                }
            }

            using var response = await _http.GetAsync(url);

            // If unauthorized, try to re-login once
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _isAuthenticated = false;

                // Try form login
                if (!await LoginAsync())
                {
                    // Form login failed, try Basic Auth
                    return await GetGatewayApiInfoWithBasicAuthAsync();
                }

                using var retryResponse = await _http.GetAsync(url);
                if (!retryResponse.IsSuccessStatusCode)
                    return await GetGatewayApiInfoWithBasicAuthAsync();

                var retryJson = await retryResponse.Content.ReadAsStringAsync();
                return ParseSystemPerformance(retryJson);
            }

            if (!response.IsSuccessStatusCode)
                return await GetGatewayApiInfoWithBasicAuthAsync();

            var json = await response.Content.ReadAsStringAsync();
            return ParseSystemPerformance(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fallback: Try to get gateway info using Basic Authentication directly on the API endpoint.
    /// </summary>
    private async Task<GatewayApiInfo?> GetGatewayApiInfoWithBasicAuthAsync()
    {
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
            return null;

        try
        {
            using var client = new HttpClient { Timeout = _http.Timeout };
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var url = new Uri(_gatewayBase, "data/api/v1/systemPerformance/currentGauges");
            using var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return ParseSystemPerformance(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the systemPerformance/currentGauges response.
    /// Example: {"cpu":24.66,"heapMemory":1195661312,"maxMemory":2147483648}
    /// </summary>
    private static GatewayApiInfo? ParseSystemPerformance(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var info = new GatewayApiInfo();

            if (root.TryGetProperty("cpu", out var cpu))
                info.CpuUsagePercent = cpu.GetDouble();

            if (root.TryGetProperty("heapMemory", out var heapMem))
                info.HeapMemoryBytes = heapMem.GetInt64();

            if (root.TryGetProperty("maxMemory", out var maxMem))
                info.MaxMemoryBytes = maxMem.GetInt64();

            return info;
        }
        catch
        {
            return null;
        }
    }

    public async Task<GatewaySnapshot> GetGatewaySnapshotAsync()
    {
        var svcStatus = GetServiceStatusSafe(ServiceName);
        var (gwOk, gwInfo) = await CheckGatewayAsync();

        // If session auth is enabled and gateway is OK, fetch performance metrics
        GatewayApiInfo? perfInfo = null;
        if (_useSessionAuth && gwOk)
        {
            perfInfo = await GetGatewayApiInfoAsync();
        }

        return new GatewaySnapshot(svcStatus, gwOk, gwInfo, perfInfo);
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
        _handler?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Gateway performance information from /data/api/v1/systemPerformance/currentGauges.
/// Response: {"cpu":24.66,"heapMemory":1195661312,"maxMemory":2147483648}
/// </summary>
public sealed class GatewayApiInfo
{
    /// <summary>CPU usage percentage (0-100)</summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>Current heap memory usage in bytes</summary>
    public long HeapMemoryBytes { get; set; }

    /// <summary>Maximum heap memory available in bytes</summary>
    public long MaxMemoryBytes { get; set; }

    public string HeapMemoryMB => $"{HeapMemoryBytes / 1024 / 1024} MB";
    public string MaxMemoryMB => $"{MaxMemoryBytes / 1024 / 1024} MB";

    public double MemoryUsagePercent => MaxMemoryBytes > 0
        ? Math.Round((double)HeapMemoryBytes / MaxMemoryBytes * 100, 1)
        : 0;

    public override string ToString()
    {
        var parts = new List<string>();

        if (CpuUsagePercent > 0)
            parts.Add($"CPU: {CpuUsagePercent:F1}%");

        if (MaxMemoryBytes > 0)
            parts.Add($"Heap: {MemoryUsagePercent}%");

        return parts.Count > 0 ? string.Join(" | ", parts) : "OK";
    }
}

public sealed record GatewaySnapshot(
    ServiceControllerStatus ServiceStatus,
    bool GatewayOk,
    string GatewayInfo,
    GatewayApiInfo? PerformanceInfo = null)
{
    public bool CanStart => ServiceStatus is ServiceControllerStatus.Stopped;
    public bool CanStop => ServiceStatus is ServiceControllerStatus.Running;
    public bool CanRestart => ServiceStatus is ServiceControllerStatus.Running;

    /// <summary>
    /// Returns true if performance metrics are available.
    /// </summary>
    public bool HasPerformanceInfo => PerformanceInfo != null && PerformanceInfo.MaxMemoryBytes > 0;
}
