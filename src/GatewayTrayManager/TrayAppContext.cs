using Microsoft.Extensions.Configuration;
using System;
using System.Drawing;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServiceManager;
using Timer = System.Windows.Forms.Timer;

namespace GatewayTrayManager;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _miService;
    private readonly ToolStripMenuItem _miGateway;
    private readonly ToolStripMenuItem _miStart;
    private readonly ToolStripMenuItem _miStop;
    private readonly ToolStripMenuItem _miRestart;

    private readonly Timer _timer;
    private readonly GatewayMonitor _monitor;
    private readonly string _serviceName;
    private readonly string _gatewayUrl;
    private readonly SynchronizationContext? _syncContext;

    private bool _lastGatewayOk;
    private ServiceControllerStatus? _lastServiceStatus;
    private bool _isDisposed;
    private bool _isRefreshing;

    public TrayAppContext()
    {
        _syncContext = SynchronizationContext.Current;
        var cfg = LoadConfig();
        _serviceName = cfg.ServiceName;
        _gatewayUrl = cfg.GatewayBaseUrl;

        _monitor = new GatewayMonitor(
            serviceName: cfg.ServiceName,
            gatewayBaseUrl: cfg.GatewayBaseUrl,
            timeoutSeconds: cfg.HttpTimeoutSeconds,
            username: cfg.Username,
            password: cfg.Password
        );

        _miService = new ToolStripMenuItem("🖥️ Service: (loading...)") { Enabled = false };
        _miGateway = new ToolStripMenuItem("🌐 Gateway: (loading...)") { Enabled = false };

        _miStart = new ToolStripMenuItem("▶️ Start", null, (_, __) => ShowServiceOperation(GatewayServiceOperationForm.ServiceAction.Start));
        _miStop = new ToolStripMenuItem("⏹️ Stop", null, (_, __) => ShowServiceOperation(GatewayServiceOperationForm.ServiceAction.Stop));
        _miRestart = new ToolStripMenuItem("🔄 Restart", null, (_, __) => ShowServiceOperation(GatewayServiceOperationForm.ServiceAction.Restart));

        var menu = new ContextMenuStrip();
        menu.Items.Add(_miService);
        menu.Items.Add(_miGateway);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_miStart);
        menu.Items.Add(_miStop);
        menu.Items.Add(_miRestart);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("🌍 Open Gateway", null, (_, __) => OpenGateway(cfg.GatewayBaseUrl)));
        menu.Items.Add(new ToolStripMenuItem("🔃 Refresh now", null, (_, __) => SafeRefresh()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("⚙️ Configuration...", null, (_, __) => OpenConfigForm(cfg)));
        menu.Items.Add(new ToolStripMenuItem("❌ Exit", null, (_, __) => Exit()));

        _tray = new NotifyIcon
        {
            Text = "Gateway Tray Manager",
            Icon = TrayIconGenerator.CreateIcon(TrayIconGenerator.IconState.Warning), // Initial icon
            Visible = true,
            ContextMenuStrip = menu
        };

        _tray.DoubleClick += (_, __) => SafeRefresh();

        _timer = new Timer { Interval = Math.Max(1000, cfg.PollIntervalMs) };
        _timer.Tick += (_, __) => SafeRefresh(showBalloonOnChange: true);
        _timer.Start();

        SafeRefresh();
    }

    private void SafeRefresh(bool showBalloonOnChange = false)
    {
        // Prevent concurrent refreshes
        if (_isDisposed || _isRefreshing) return;

        _isRefreshing = true;

        // Fire and forget with error handling to prevent crashes
        _ = Task.Run(async () =>
        {
            try
            {
                if (_isDisposed) return;
                await RefreshAsync(showBalloonOnChange);
            }
            catch (ObjectDisposedException)
            {
                // App is closing, ignore
            }
            catch (Exception ex)
            {
                // Log error silently - don't crash the app
                System.Diagnostics.Debug.WriteLine($"Refresh error: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
            }
        });
    }

    private static AppConfig LoadConfig()
    {
        // appsettings.json will be copied to output directory
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        var cfg = builder.Build();
        var appCfg = cfg.GetSection("Gateway").Get<AppConfig>() ?? new AppConfig();

        // Default safe values
        if (string.IsNullOrWhiteSpace(appCfg.ServiceName)) appCfg.ServiceName = "Gateway";
        if (string.IsNullOrWhiteSpace(appCfg.GatewayBaseUrl)) appCfg.GatewayBaseUrl = "http://localhost:8088";
        if (appCfg.PollIntervalMs <= 0) appCfg.PollIntervalMs = 3000;
        if (appCfg.HttpTimeoutSeconds <= 0) appCfg.HttpTimeoutSeconds = 2;

        return appCfg;
    }

    private static void OpenGateway(string baseUrl)
    {
        try
        {
            var url = baseUrl.TrimEnd('/') + "/";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore
        }
    }

    private void OpenConfigForm(AppConfig currentConfig)
    {
        _timer.Stop();
        try
        {
            using var form = new ConfigForm(currentConfig);
            form.ShowDialog();
        }
        finally
        {
            _timer.Start();
        }
    }

    private void ShowServiceOperation(GatewayServiceOperationForm.ServiceAction action)
    {
        _timer.Stop();
        try
        {
            using var form = new GatewayServiceOperationForm(_serviceName, _gatewayUrl, action, _tray);
            form.ShowDialog();
        }
        finally
        {
            SafeRefresh();
            _timer.Start();
        }
    }

    private async Task RefreshAsync(bool showBalloonOnChange)
    {
        if (_isDisposed) return;

        try
        {
            var snapshot = await _monitor.GetGatewaySnapshotAsync();

            if (_isDisposed) return;

            // Use SynchronizationContext or Invoke to safely update UI from background thread
            InvokeOnUIThread(() => UpdateUIFromSnapshot(snapshot, showBalloonOnChange));
        }
        catch (ObjectDisposedException)
        {
            // App is closing, ignore
        }
        catch
        {
            if (_isDisposed) return;

            // Handle errors in UI thread
            InvokeOnUIThread(UpdateUIOnError);
        }
    }

    private void InvokeOnUIThread(Action action)
    {
        if (_isDisposed) return;

        try
        {
            if (_syncContext != null)
            {
                _syncContext.Post(_ => 
                {
                    if (!_isDisposed)
                    {
                        try { action(); } catch { }
                    }
                }, null);
            }
            else if (_tray.ContextMenuStrip?.InvokeRequired == true)
            {
                _tray.ContextMenuStrip.Invoke(action);
            }
            else
            {
                action();
            }
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private void UpdateUIFromSnapshot(GatewaySnapshot snapshot, bool showBalloonOnChange)
    {
        if (_isDisposed) return;

        try
        {
            _miService.Text = $"🖥️ Service: {snapshot.ServiceStatus}";
            _miGateway.Text = snapshot.GatewayOk
                ? $"🌐 Gateway: OK ({snapshot.GatewayInfo})"
                : $"🌐 Gateway: FAIL ({snapshot.GatewayInfo})";

            _miStart.Enabled = snapshot.CanStart;
            _miStop.Enabled = snapshot.CanStop;
            _miRestart.Enabled = snapshot.CanRestart;

            // Tooltip (max ~63 chars, keep short)
            _tray.Text = $"Gateway {snapshot.ServiceStatus} | GW {(snapshot.GatewayOk ? "OK" : "FAIL")}";

            // Icon - Gateway style flame with status indicator
            var iconState = snapshot.ServiceStatus switch
            {
                ServiceControllerStatus.Running when snapshot.GatewayOk => TrayIconGenerator.IconState.Running,
                ServiceControllerStatus.Running => TrayIconGenerator.IconState.Warning,
                ServiceControllerStatus.StartPending or ServiceControllerStatus.StopPending => TrayIconGenerator.IconState.Warning,
                ServiceControllerStatus.Stopped => TrayIconGenerator.IconState.Stopped,
                _ => TrayIconGenerator.IconState.Error
            };
            _tray.Icon = TrayIconGenerator.CreateIcon(iconState);

            if (showBalloonOnChange)
            {
                var svcChanged = _lastServiceStatus != snapshot.ServiceStatus;
                var gwChanged = _lastGatewayOk != snapshot.GatewayOk;

                if (_lastServiceStatus is null)
                {
                    // first run - no balloon
                }
                else if (svcChanged || gwChanged)
                {
                    var title = "Gateway status changed";
                    var msg = $"Service: {snapshot.ServiceStatus} | Gateway: {(snapshot.GatewayOk ? "OK" : "FAIL")} ({snapshot.GatewayInfo})";
                    var tipIcon = (snapshot.ServiceStatus == ServiceControllerStatus.Running && snapshot.GatewayOk) ? ToolTipIcon.Info : ToolTipIcon.Warning;
                    _tray.ShowBalloonTip(3000, title, msg, tipIcon);
                }
            }

            _lastServiceStatus = snapshot.ServiceStatus;
            _lastGatewayOk = snapshot.GatewayOk;
        }
        catch
        {
            // Errors handled in RefreshAsync
        }
    }

    private void UpdateUIOnError()
    {
        try
        {
            _miService.Text = "🖥️ Service: (error)";
            _miGateway.Text = "🌐 Gateway: (error)";
            _tray.Icon = TrayIconGenerator.CreateIcon(TrayIconGenerator.IconState.Error);
            _tray.Text = "Gateway Tray Manager (error)";
        }
        catch
        {
            // Ignore
        }
    }

    private void Exit()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _timer.Stop();
            _timer.Dispose();
        }
        catch { }

        try
        {
            _monitor.Dispose();
        }
        catch { }

        try
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        catch { }

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            _isDisposed = true;
            _timer?.Dispose();
            _monitor?.Dispose();
            _tray?.Dispose();
        }
        base.Dispose(disposing);
    }
}

public sealed class AppConfig
{
    public string ServiceName { get; set; } = "Gateway";
    public string GatewayBaseUrl { get; set; } = "http://localhost:8088";
    public int PollIntervalMs { get; set; } = 3000;
    public int HttpTimeoutSeconds { get; set; } = 2;

    // Optional authentication (leave empty if not required)
    public string? Username { get; set; }
    public string? Password { get; set; }

    public bool HasCredentials => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
