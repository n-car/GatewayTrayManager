using System;
using System.Drawing;
using System.Net.Http;
using System.ServiceProcess;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServiceManager;
using GatewayTrayManager.Localization;

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
        Text = string.Format(Strings.ServiceOperationTitle, action);
    }

    protected override string GetOperationText(ServiceAction action) => action switch
    {
        ServiceAction.Start => Strings.OperationStart,
        ServiceAction.Stop => Strings.OperationStop,
        ServiceAction.Restart => Strings.OperationRestart,
        _ => Strings.OperationProcessing
    };

    protected override async Task OnServiceStartedAsync()
    {
        UpdateTrayIcon(TrayIconGenerator.IconState.Warning, Strings.WaitingForGateway.Replace("🌐 ", "").TrimEnd('.'));
        await WaitForGatewayReadyAsync();
    }

    private async Task WaitForGatewayReadyAsync()
    {
        UpdateStatus(Strings.WaitingForGateway);
        UpdateDetails(Strings.CheckingStatusPing);

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
                    UpdateStatus(string.Format(Strings.GatewayStarting, dots, elapsed.ToString("F0")));
                    UpdateDetails(Strings.ServiceUnavailable503);
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
                                UpdateStatus(Strings.GatewayIsRunning);
                                UpdateDetails("");
                                UpdateTrayIcon(TrayIconGenerator.IconState.Running, Strings.GatewayRunningStatusOK);
                                return;
                            }
                            else
                            {
                                UpdateStatus(string.Format(Strings.GatewayState, state, dots, elapsed.ToString("F0")));
                                UpdateDetails(Strings.WaitingForRunning);
                                lastError = $"State: {state}";
                            }
                        }
                        else
                        {
                            UpdateStatus(Strings.GatewayIsResponding);
                            UpdateDetails("");
                            UpdateTrayIcon(TrayIconGenerator.IconState.Running, Strings.GatewayRunningStatusOK);
                            return;
                        }
                    }
                    catch
                    {
                        UpdateStatus(Strings.GatewayIsResponding);
                        UpdateDetails("");
                        UpdateTrayIcon(TrayIconGenerator.IconState.Running, Strings.GatewayRunningStatusOK);
                        return;
                    }
                }
                else
                {
                    UpdateStatus(string.Format(Strings.WaitingForGatewayDots, dots, elapsed.ToString("F0")));
                    UpdateDetails(string.Format(Strings.GatewayNotReadyYet, status));
                    lastError = $"HTTP {status}";
                }
            }
            catch (HttpRequestException ex)
            {
                UpdateStatus(string.Format(Strings.WaitingForGatewayDots, dots, elapsed.ToString("F0")));
                UpdateDetails(Strings.ConnectionRefused);
                lastError = ex.Message;
            }
            catch (TaskCanceledException) when (!Cts.Token.IsCancellationRequested)
            {
                UpdateStatus(string.Format(Strings.WaitingForGatewayDots, dots, elapsed.ToString("F0")));
                UpdateDetails(Strings.RequestTimeout);
                lastError = Strings.RequestTimeout;
            }

            await Task.Delay(1000, Cts.Token);
        }

        UpdateStatus(Strings.GatewayCheckTimeout);
        UpdateDetails(string.Format(Strings.ServiceRunningGatewayNoResponse, lastError));
        UpdateTrayIcon(TrayIconGenerator.IconState.Warning, Strings.GatewayRunningStatusTimeout);
        await Task.Delay(2000, Cts.Token);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _http.Dispose();
        base.OnFormClosing(e);
    }
}
