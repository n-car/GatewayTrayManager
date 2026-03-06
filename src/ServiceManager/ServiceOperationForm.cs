using System.Drawing;
using System.ServiceProcess;
using System.Windows.Forms;

namespace ServiceManager;

/// <summary>
/// Form for executing service operations with visual feedback.
/// </summary>
public class ServiceOperationForm : Form
{
    protected readonly Label LblOperation;
    protected readonly Label LblStatus;
    protected readonly ProgressBar ProgressBar;
    protected readonly Label LblDetails;
    protected readonly Button BtnClose;
    protected readonly System.Windows.Forms.Timer AnimTimer;
    protected readonly NotifyIcon? TrayIcon;

    protected readonly string ServiceName;
    protected readonly ServiceAction Action;
    protected readonly CancellationTokenSource Cts;

    protected bool OperationComplete;
    protected bool OperationSuccess;
    protected string ResultMessage = "";

    public enum ServiceAction { Start, Stop, Restart }

    public ServiceOperationForm(string serviceName, ServiceAction action, NotifyIcon? trayIcon = null)
    {
        ServiceName = serviceName;
        Action = action;
        Cts = new CancellationTokenSource();
        TrayIcon = trayIcon;

        Text = $"Service - {action}";
        Icon = TrayIconGenerator.CreateApplicationIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(400, 220);
        ControlBox = false;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20)
        };

        LblOperation = new Label
        {
            Text = GetOperationText(action),
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Height = 30
        };
        mainPanel.Controls.Add(LblOperation, 0, 0);

        ProgressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Height = 25
        };
        mainPanel.Controls.Add(ProgressBar, 0, 1);

        LblStatus = new Label
        {
            Text = "⏳ Initializing...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 10),
            AutoSize = false,
            Height = 30
        };
        mainPanel.Controls.Add(LblStatus, 0, 2);

        LblDetails = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray,
            AutoSize = false,
            Height = 25
        };
        mainPanel.Controls.Add(LblDetails, 0, 3);

        var buttonPanel = new Panel { Dock = DockStyle.Fill, Height = 40 };
        BtnClose = new Button
        {
            Text = "OK",
            Width = 100,
            Height = 30,
            Visible = false,
            Anchor = AnchorStyles.None
        };
        BtnClose.Click += (_, _) => Close();
        buttonPanel.Controls.Add(BtnClose);
        BtnClose.Location = new Point((buttonPanel.Width - BtnClose.Width) / 2, 5);
        buttonPanel.Resize += (_, _) => BtnClose.Location = new Point((buttonPanel.Width - BtnClose.Width) / 2, 5);
        mainPanel.Controls.Add(buttonPanel, 0, 4);

        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Controls.Add(mainPanel);

        AnimTimer = new System.Windows.Forms.Timer { Interval = 500 };
        AnimTimer.Tick += OnAnimationTick;

        Load += async (_, _) => await ExecuteOperationAsync();
    }

    protected virtual string GetOperationText(ServiceAction action) => action switch
    {
        ServiceAction.Start => "▶️ Starting Service...",
        ServiceAction.Stop => "⏹️ Stopping Service...",
        ServiceAction.Restart => "🔄 Restarting Service...",
        _ => "Processing..."
    };

    protected virtual async Task ExecuteOperationAsync()
    {
        AnimTimer.Start();
        var startTime = DateTime.Now;

        try
        {
            // Check if we need elevation
            if (!ElevationHelper.IsRunningAsAdmin())
            {
                // Run elevated and wait for result
                await ExecuteWithElevationAsync();
            }
            else
            {
                // Already admin, execute directly
                await Task.Run(async () =>
                {
                    using var sc = new ServiceController(ServiceName);

                    switch (Action)
                    {
                        case ServiceAction.Start:
                            await StartServiceAsync(sc);
                            break;
                        case ServiceAction.Stop:
                            await StopServiceAsync(sc);
                            break;
                        case ServiceAction.Restart:
                            await RestartServiceAsync(sc);
                            break;
                    }
                }, Cts.Token);
            }

            var elapsed = DateTime.Now - startTime;
            OperationSuccess = true;
            ResultMessage = $"✅ Operation completed successfully in {elapsed.TotalSeconds:F1}s";
        }
        catch (OperationCanceledException)
        {
            OperationSuccess = false;
            ResultMessage = "⚠️ Operation cancelled";
        }
        catch (ElevationRequiredException ex)
        {
            OperationSuccess = false;
            ResultMessage = ex.Message;
        }
        catch (Exception ex)
        {
            OperationSuccess = false;
            ResultMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            OperationComplete = true;
            AnimTimer.Stop();
            UpdateUI();
        }
    }

    /// <summary>
    /// Executes the service operation using UAC elevation.
    /// </summary>
    protected virtual async Task ExecuteWithElevationAsync()
    {
        var actionName = Action switch
        {
            ServiceAction.Start => "start",
            ServiceAction.Stop => "stop",
            ServiceAction.Restart => "restart",
            _ => throw new ArgumentException("Unknown action")
        };

        UpdateStatus("🔐 Requesting administrator privileges...");
        UpdateDetails("A UAC prompt will appear. Click Yes to continue.");

        var success = await Task.Run(() =>
        {
            return ServiceControlHelper.ExecuteWithElevation(actionName, ServiceName);
        }, Cts.Token);

        if (!success)
        {
            throw new ElevationRequiredException("❌ Operation cancelled or failed. Administrator privileges are required.");
        }

        // After elevation, verify the service status
        using var sc = new ServiceController(ServiceName);
        sc.Refresh();

        var expectedStatus = Action switch
        {
            ServiceAction.Start => ServiceControllerStatus.Running,
            ServiceAction.Stop => ServiceControllerStatus.Stopped,
            ServiceAction.Restart => ServiceControllerStatus.Running,
            _ => sc.Status
        };

        if (sc.Status == expectedStatus)
        {
            UpdateStatus($"✅ Service is now {sc.Status}");

            // Update tray icon based on final status
            if (sc.Status == ServiceControllerStatus.Running)
            {
                await OnServiceStartedAsync();
            }
            else if (sc.Status == ServiceControllerStatus.Stopped)
            {
                UpdateTrayIcon(TrayIconGenerator.IconState.Stopped, "Service stopped");
            }
        }
        else
        {
            throw new Exception($"Service status is {sc.Status}, expected {expectedStatus}");
        }
    }

    protected virtual async Task StartServiceAsync(ServiceController sc)
    {
        UpdateStatus("🔍 Checking current status...");
        UpdateTrayIcon(TrayIconGenerator.IconState.Warning, "Service starting...");
        sc.Refresh();

        if (sc.Status == ServiceControllerStatus.Running)
        {
            UpdateStatus("ℹ️ Service is already running");
            await Task.Delay(500);
            return;
        }

        if (sc.Status == ServiceControllerStatus.StartPending)
        {
            UpdateStatus("⏳ Service is already starting, waiting...");
        }
        else
        {
            UpdateStatus("▶️ Sending start command...");
            sc.Start();
        }

        await WaitForStatusAsync(sc, ServiceControllerStatus.Running, "Starting");
        await OnServiceStartedAsync();
    }

    protected virtual async Task StopServiceAsync(ServiceController sc)
    {
        UpdateStatus("🔍 Checking current status...");
        UpdateTrayIcon(TrayIconGenerator.IconState.Warning, "Service stopping...");
        sc.Refresh();

        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            UpdateStatus("ℹ️ Service is already stopped");
            UpdateTrayIcon(TrayIconGenerator.IconState.Stopped, "Service stopped");
            await Task.Delay(500);
            return;
        }

        if (sc.Status == ServiceControllerStatus.StopPending)
        {
            UpdateStatus("⏳ Service is already stopping, waiting...");
        }
        else
        {
            UpdateStatus("⏹️ Sending stop command...");
            sc.Stop();
        }

        await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, "Stopping");
        UpdateTrayIcon(TrayIconGenerator.IconState.Stopped, "Service stopped");
    }

    protected virtual async Task RestartServiceAsync(ServiceController sc)
    {
        UpdateStatus("🔍 Checking current status...");
        UpdateTrayIcon(TrayIconGenerator.IconState.Warning, "Service restarting...");
        sc.Refresh();

        if (sc.Status != ServiceControllerStatus.Stopped)
        {
            if (sc.Status != ServiceControllerStatus.StopPending)
            {
                UpdateStatus("⏹️ Stopping service...");
                sc.Stop();
            }

            await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, "Stopping");
        }

        UpdateStatus("⏳ Waiting for resources to release...");
        UpdateTrayIcon(TrayIconGenerator.IconState.Stopped, "Service stopped (restarting)");
        await Task.Delay(1500, Cts.Token);

        UpdateStatus("▶️ Starting service...");
        UpdateTrayIcon(TrayIconGenerator.IconState.Warning, "Service starting...");
        sc.Start();

        await WaitForStatusAsync(sc, ServiceControllerStatus.Running, "Starting");
        await OnServiceStartedAsync();
    }

    /// <summary>
    /// Called after the service has started. Override to add custom health checks.
    /// </summary>
    protected virtual Task OnServiceStartedAsync()
    {
        UpdateTrayIcon(TrayIconGenerator.IconState.Running, "Service running");
        return Task.CompletedTask;
    }

    protected async Task WaitForStatusAsync(ServiceController sc, ServiceControllerStatus targetStatus, string actionVerb)
    {
        var timeout = TimeSpan.FromSeconds(90);
        var startTime = DateTime.Now;
        var dotCount = 0;

        while (DateTime.Now - startTime < timeout)
        {
            Cts.Token.ThrowIfCancellationRequested();

            sc.Refresh();
            var elapsed = (DateTime.Now - startTime).TotalSeconds;
            var dots = new string('.', (dotCount++ % 3) + 1);

            UpdateStatus($"⏳ {actionVerb}{dots} ({sc.Status}) - {elapsed:F0}s");
            UpdateDetails($"Waiting for {targetStatus}...");

            if (sc.Status == targetStatus)
            {
                UpdateStatus($"✅ Service is now {targetStatus}");
                UpdateDetails("");
                return;
            }

            await Task.Delay(500, Cts.Token);
        }

        throw new System.TimeoutException($"Operation timed out after {timeout.TotalSeconds}s. Current status: {sc.Status}");
    }

    protected void UpdateStatus(string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => LblStatus.Text = text);
        }
        else
        {
            LblStatus.Text = text;
        }
    }

    protected void UpdateDetails(string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => LblDetails.Text = text);
        }
        else
        {
            LblDetails.Text = text;
        }
    }

    protected void UpdateTrayIcon(TrayIconGenerator.IconState state, string? tooltip = null)
    {
        if (TrayIcon == null) return;

        try
        {
            if (InvokeRequired)
            {
                Invoke(() => UpdateTrayIconInternal(state, tooltip));
            }
            else
            {
                UpdateTrayIconInternal(state, tooltip);
            }
        }
        catch
        {
            // Ignore errors updating tray icon
        }
    }

    private void UpdateTrayIconInternal(TrayIconGenerator.IconState state, string? tooltip)
    {
        if (TrayIcon == null) return;

        TrayIcon.Icon = TrayIconGenerator.CreateIcon(state);
        if (tooltip != null)
        {
            TrayIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
        }
    }

    protected virtual void OnAnimationTick(object? sender, EventArgs e)
    {
        // Keep UI responsive during operation
    }

    protected virtual void UpdateUI()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateUI);
            return;
        }

        ProgressBar.Style = ProgressBarStyle.Continuous;
        ProgressBar.Value = 100;

        if (OperationSuccess)
        {
            LblOperation.Text = Action switch
            {
                ServiceAction.Start => "✅ Service Started",
                ServiceAction.Stop => "✅ Service Stopped",
                ServiceAction.Restart => "✅ Service Restarted",
                _ => "✅ Operation Complete"
            };
            LblOperation.ForeColor = Color.Green;
        }
        else
        {
            LblOperation.Text = "❌ Operation Failed";
            LblOperation.ForeColor = Color.Red;
        }

        LblStatus.Text = ResultMessage;
        LblDetails.Text = "";

        BtnClose.Visible = true;
        BtnClose.Enabled = true;
        BtnClose.Focus();
        ControlBox = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!OperationComplete)
        {
            e.Cancel = true;
            return;
        }

        AnimTimer.Stop();
        AnimTimer.Dispose();
        Cts.Dispose();
        base.OnFormClosing(e);
    }
}

/// <summary>
/// Exception thrown when an operation requires elevation but was cancelled or failed.
/// </summary>
public class ElevationRequiredException : Exception
{
    public ElevationRequiredException(string message) : base(message) { }
}
