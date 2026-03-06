using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using ServiceManager;

namespace GatewayTrayManager;

public sealed class ConfigForm : Form
{
    private const string AppName = "Gateway Tray Manager";
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly TextBox _txtServiceName;
    private readonly TextBox _txtGatewayUrl;
    private readonly TextBox _txtUsername;
    private readonly TextBox _txtPassword;
    private readonly NumericUpDown _numPollInterval;
    private readonly NumericUpDown _numTimeout;
    private readonly CheckBox _chkAutoStart;
    private readonly RichTextBox _txtTestResult;
    private readonly Button _btnTest;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly HttpClient _http;

    public AppConfig Config { get; private set; }
    public bool ConfigSaved { get; private set; }

    public ConfigForm(AppConfig currentConfig)
    {
        Config = currentConfig;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        Text = "Gateway Tray Manager - Configuration";
        Icon = TrayIconGenerator.CreateApplicationIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(500, 540);
        Padding = new Padding(10);

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(10)
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Service Name
        mainPanel.Controls.Add(new Label { Text = "Service Name:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _txtServiceName = new TextBox { Dock = DockStyle.Fill, Text = currentConfig.ServiceName };
        mainPanel.Controls.Add(_txtServiceName, 1, 0);

        // Gateway URL
        mainPanel.Controls.Add(new Label { Text = "Gateway URL:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _txtGatewayUrl = new TextBox { Dock = DockStyle.Fill, Text = currentConfig.GatewayBaseUrl };
        mainPanel.Controls.Add(_txtGatewayUrl, 1, 1);

        // Poll Interval
        mainPanel.Controls.Add(new Label { Text = "Poll Interval (ms):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _numPollInterval = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1000,
            Maximum = 60000,
            Value = Math.Clamp(currentConfig.PollIntervalMs, 1000, 60000),
            Increment = 500
        };
        mainPanel.Controls.Add(_numPollInterval, 1, 2);

        // Timeout
        mainPanel.Controls.Add(new Label { Text = "HTTP Timeout (s):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        _numTimeout = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 30,
            Value = Math.Clamp(currentConfig.HttpTimeoutSeconds, 1, 30)
        };
        mainPanel.Controls.Add(_numTimeout, 1, 3);

        // Username (optional)
        mainPanel.Controls.Add(new Label { Text = "Username:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        _txtUsername = new TextBox { Dock = DockStyle.Fill, Text = currentConfig.Username ?? "", PlaceholderText = "(optional)" };
        mainPanel.Controls.Add(_txtUsername, 1, 4);

        // Password (optional)
        mainPanel.Controls.Add(new Label { Text = "Password:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
        _txtPassword = new TextBox { Dock = DockStyle.Fill, Text = currentConfig.Password ?? "", PlaceholderText = "(optional)", UseSystemPasswordChar = true };
        mainPanel.Controls.Add(_txtPassword, 1, 5);

        // Auto-start checkbox
        _chkAutoStart = new CheckBox
        {
            Text = "🚀 Start automatically with Windows",
            Dock = DockStyle.Fill,
            Checked = IsAutoStartEnabled(),
            AutoSize = true
        };
        mainPanel.Controls.Add(_chkAutoStart, 0, 6);
        mainPanel.SetColumnSpan(_chkAutoStart, 2);

        // Test Button
        _btnTest = new Button
        {
            Text = "🔍 Test Connection",
            Dock = DockStyle.Fill,
            Height = 35
        };
        _btnTest.Click += OnTestButtonClick;
        var testPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 5) };
        testPanel.Controls.Add(_btnTest);
        mainPanel.Controls.Add(testPanel, 0, 7);
        mainPanel.SetColumnSpan(testPanel, 2);

        // Test Result
        _txtTestResult = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.LightGreen
        };
        mainPanel.Controls.Add(_txtTestResult, 0, 8);
        mainPanel.SetColumnSpan(_txtTestResult, 2);
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 0 - Service Name
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 1 - Gateway URL
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 2 - Poll Interval
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 3 - Timeout
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 4 - Username
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 5 - Password
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 6 - Auto-start checkbox
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Row 7 - Test button
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Row 8 - Test result
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Row 9 - Buttons

        // Buttons Panel
        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 5, 0, 0)
        };

        _btnCancel = new Button { Text = "Cancel", Width = 80, Height = 30 };
        _btnCancel.Click += (_, _) => { ConfigSaved = false; Close(); };

        _btnSave = new Button { Text = "💾 Save", Width = 80, Height = 30 };
        _btnSave.Click += (_, _) => SaveConfig();

        buttonsPanel.Controls.Add(_btnCancel);
        buttonsPanel.Controls.Add(_btnSave);

        mainPanel.Controls.Add(buttonsPanel, 0, 9);
        mainPanel.SetColumnSpan(buttonsPanel, 2);

        Controls.Add(mainPanel);

        // Set tab order
        _txtServiceName.TabIndex = 0;
        _txtGatewayUrl.TabIndex = 1;
        _numPollInterval.TabIndex = 2;
        _numTimeout.TabIndex = 3;
        _txtUsername.TabIndex = 4;
        _txtPassword.TabIndex = 5;
        _chkAutoStart.TabIndex = 6;
        _btnTest.TabIndex = 7;
        _btnSave.TabIndex = 8;
        _btnCancel.TabIndex = 9;
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            var value = key?.GetValue(AppName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    private static void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            if (enabled)
            {
                // Use Environment.ProcessPath for single-file apps, fallback to BaseDirectory
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Path.Combine(AppContext.BaseDirectory, "GatewayTrayManager.exe");
                }
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Ignore registry errors
        }
    }

    private async void OnTestButtonClick(object? sender, EventArgs e)
    {
        try
        {
            await TestConnectionAsync();
        }
        catch (Exception ex)
        {
            AppendResult($"\n❌ Unexpected error: {ex.Message}", Color.OrangeRed);
            _btnTest.Enabled = true;
        }
    }

    private async Task TestConnectionAsync()
    {
        _btnTest.Enabled = false;
        _txtTestResult.Clear();
        _txtTestResult.ForeColor = Color.White;

        var baseUrl = _txtGatewayUrl.Text.Trim().TrimEnd('/') + "/";

        try
        {
            AppendResult($"Testing connection to: {baseUrl}");
            AppendResult(new string('-', 40));

            // Test 1: StatusPing
            AppendResult("\n[1] Testing /StatusPing ...");
            var (pingOk, pingInfo) = await TestEndpointAsync(baseUrl, "StatusPing");
            if (pingOk)
            {
                AppendResult($"    ✅ StatusPing OK", Color.LightGreen);
                AppendResult($"    Response: {pingInfo}");
            }
            else
            {
                AppendResult($"    ❌ StatusPing FAILED: {pingInfo}", Color.OrangeRed);
            }

            // Test 2: gwinfo
            AppendResult("\n[2] Testing /system/gwinfo ...");
            var (gwinfoOk, gwinfoInfo) = await TestEndpointAsync(baseUrl, "system/gwinfo");
            if (gwinfoOk)
            {
                AppendResult($"    ✅ gwinfo OK", Color.LightGreen);
                AppendResult($"    Response: {TruncateResponse(gwinfoInfo, 200)}");
            }
            else
            {
                AppendResult($"    ❌ gwinfo FAILED: {gwinfoInfo}", Color.OrangeRed);
            }

            // Test 3: Service
            AppendResult($"\n[3] Testing Service: {_txtServiceName.Text} ...");
            var (svcOk, svcInfo) = TestService(_txtServiceName.Text);
            if (svcOk)
            {
                AppendResult($"    ✅ Service OK: {svcInfo}", Color.LightGreen);
            }
            else
            {
                AppendResult($"    ❌ Service FAILED: {svcInfo}", Color.OrangeRed);
            }

            // Summary
            AppendResult("\n" + new string('=', 40));
            if (pingOk && svcOk)
            {
                AppendResult("✅ All tests passed! Configuration is valid.", Color.LightGreen);
            }
            else
            {
                AppendResult("⚠️ Some tests failed. Check your configuration.", Color.Yellow);
            }
        }
        catch (Exception ex)
        {
            AppendResult($"\n❌ Error: {ex.Message}", Color.OrangeRed);
        }
        finally
        {
            _btnTest.Enabled = true;
        }
    }

    private async Task<(bool ok, string info)> TestEndpointAsync(string baseUrl, string endpoint)
    {
        try
        {
            var url = new Uri(new Uri(baseUrl), endpoint);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req);

            var status = (int)resp.StatusCode;
            var content = await resp.Content.ReadAsStringAsync();

            if (status >= 200 && status < 400)
            {
                return (true, content);
            }

            return (false, $"{status} {resp.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static (bool ok, string info) TestService(string serviceName)
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController(serviceName);
            sc.Refresh();
            return (true, sc.Status.ToString());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private void AppendResult(string text, Color? color = null)
    {
        var start = _txtTestResult.TextLength;
        _txtTestResult.AppendText(text + Environment.NewLine);

        if (color.HasValue)
        {
            _txtTestResult.Select(start, text.Length);
            _txtTestResult.SelectionColor = color.Value;
            _txtTestResult.Select(_txtTestResult.TextLength, 0);
        }
    }

    private static string TruncateResponse(string response, int maxLength)
    {
        if (string.IsNullOrEmpty(response)) return "(empty)";
        if (response.Length <= maxLength) return response;
        return response[..maxLength] + "...";
    }

    private void SaveConfig()
    {
        if (string.IsNullOrWhiteSpace(_txtServiceName.Text))
        {
            MessageBox.Show("Service Name is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtGatewayUrl.Text))
        {
            MessageBox.Show("Gateway URL is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!Uri.TryCreate(_txtGatewayUrl.Text, UriKind.Absolute, out _))
        {
            MessageBox.Show("Gateway URL is not a valid URL.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Config = new AppConfig
        {
            ServiceName = _txtServiceName.Text.Trim(),
            GatewayBaseUrl = _txtGatewayUrl.Text.Trim(),
            PollIntervalMs = (int)_numPollInterval.Value,
            HttpTimeoutSeconds = (int)_numTimeout.Value,
            Username = string.IsNullOrWhiteSpace(_txtUsername.Text) ? null : _txtUsername.Text.Trim(),
            Password = string.IsNullOrWhiteSpace(_txtPassword.Text) ? null : _txtPassword.Text
        };

        // Save to appsettings.json
        try
        {
            var json = JsonSerializer.Serialize(new { Gateway = Config }, new JsonSerializerOptions { WriteIndented = true });
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            System.IO.File.WriteAllText(path, json);

            // Save auto-start setting to registry
            SetAutoStart(_chkAutoStart.Checked);

            ConfigSaved = true;

            var autoStartMsg = _chkAutoStart.Checked 
                ? "\n\n✅ Auto-start with Windows: Enabled" 
                : "\n\n❌ Auto-start with Windows: Disabled";

            var authMsg = Config.HasCredentials
                ? "\n🔐 Authentication: Configured"
                : "";

            MessageBox.Show($"Configuration saved successfully!{autoStartMsg}{authMsg}\n\nRestart the application to apply other changes.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save configuration:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}
