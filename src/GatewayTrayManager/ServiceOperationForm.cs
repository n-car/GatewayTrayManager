using System;
using System.Drawing;
using System.Net.Http;
using System.ServiceProcess;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServiceManager;

namespace GatewayTrayManager;

/// <summary>
/// Gateway-specific service operation form with HTTP health check after start.
/// </summary>
public sealed class GatewayServiceOperationForm : ServiceOperationForm
{
    private readonly HttpClient _http;
    private readonly string _gatewayUrl;

    public GatewayServiceOperationForm(string serviceName, string gatewayUrl, ServiceAction action, NotifyIcon? trayIcon = null)
        : base(serviceName, action, trayIcon)
    {
        _gatewayUrl = gatewayUrl.TrimEnd('/') + "/";
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        Text = $"Gateway Service - {action}";
    }

    protected override string GetOperationText(ServiceAction action) => action switch
    {
        ServiceAction.Start => "▶️ Starting Gateway Service...",
        ServiceAction.Stop => "⏹️ Stopping Gateway Service...",
        ServiceAction.Restart => "🔄 Restarting Gateway Service...",
        _ => "Processing..."
    };

    protected override async Task OnServiceStartedAsync()
    {
        UpdateTrayIcon(TrayIconGenerator.IconState.Warning, "Gateway starting...");
        await WaitForGatewayReadyAsync();
    }

    private async Task WaitForGatewayReadyAsync()
    {
        UpdateStatus("🌐 Waiting for Gateway to be ready...");
        UpdateDetails("Checking /StatusPing endpoint...");

        var timeout = TimeSpan.FromSeconds(120);
        var startTime = DateTime.Now;
        var dotCount = 0;
        var lastError = "";

        while (DateTime.Now - startTime < timeout)
        {
            Cts.Token.ThrowIfCancellationRequested();

            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            var dots = new string('.', (dotCount++ % 3) + 1);

            try
            {
                var url = new Uri(new Uri(_gatewayUrl), "StatusPing");
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                using var resp = await _http.SendAsync(req, Cts.Token);

                var status = (int)resp.StatusCode;

                if (status == 503)
                {
                    UpdateStatus($"🌐 Gateway starting{dots} - {elapsed:F0}s");
                    UpdateDetails("Service Unavailable (503) - Gateway is initializing...");
                    lastError = "503 Service Unavailable";
                }
                else if (status >= 200 && status < 400)
                {
                    var content = await resp.Content.ReadAsStringAsync(Cts.Token);

                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("state", out var stateElement))
                        {
                            var state = stateElement.GetString();
                            if (state == "RUNNING")
                            {
                                UpdateStatus("✅ Gateway is RUNNING");
                                UpdateDetails("");
                                UpdateTrayIcon(TrayIconGenerator.IconState.Running, "Gateway running | Status OK");
                                return;
                            }
                            else
                            {
                                UpdateStatus($"🌐 Gateway state: {state}{dots} - {elapsed:F0}s");
                                UpdateDetails($"Waiting for RUNNING state...");
                                lastError = $"State: {state}";
                            }
                        }
                        else
                        {
                            UpdateStatus("✅ Gateway is responding");
                            UpdateDetails("");
                            UpdateTrayIcon(TrayIconGenerator.IconState.Running, "Gateway running | Status OK");
                            return;
                        }
                    }
                    catch
                    {
                        UpdateStatus("✅ Gateway is responding");
                        UpdateDetails("");
                        UpdateTrayIcon(TrayIconGenerator.IconState.Running, "Gateway running | Status OK");
                        return;
                    }
                }
                else
                {
                    UpdateStatus($"🌐 Waiting for Gateway{dots} - {elapsed:F0}s");
                    UpdateDetails($"HTTP {status} - Gateway not ready yet...");
                    lastError = $"HTTP {status}";
                }
            }
            catch (HttpRequestException ex)
            {
                UpdateStatus($"🌐 Waiting for Gateway{dots} - {elapsed:F0}s");
                UpdateDetails("Connection refused - Gateway not listening yet...");
                lastError = ex.Message;
            }
            catch (TaskCanceledException) when (!Cts.Token.IsCancellationRequested)
            {
                UpdateStatus($"🌐 Waiting for Gateway{dots} - {elapsed:F0}s");
                UpdateDetails("Request timeout - Gateway slow to respond...");
                lastError = "Request timeout";
            }

            await Task.Delay(1000, Cts.Token);
        }

        UpdateStatus("⚠️ Gateway check timed out");
        UpdateDetails($"Service is running but gateway didn't respond. Last error: {lastError}");
        UpdateTrayIcon(TrayIconGenerator.IconState.Warning, "Gateway running | Status timeout");
        await Task.Delay(2000, Cts.Token);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _http.Dispose();
        base.OnFormClosing(e);
    }
}
