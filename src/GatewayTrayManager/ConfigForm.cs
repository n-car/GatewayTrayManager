using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using ServiceManager;
using GatewayTrayManager.Localization;
using GatewayTrayManager.Security;

namespace GatewayTrayManager;

public sealed class ConfigForm : Form
{
    private const string AppName = "Gateway Tray Manager";
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Use HKLM for auto-start (same as installer) - requires admin rights
    private static readonly RegistryKey StartupRegistryRoot = Registry.LocalMachine;

    private readonly TextBox _txtServiceName;
    private readonly TextBox _txtGatewayUrl;
    private readonly TextBox _txtUsername;
    private readonly TextBox _txtPassword;
    private readonly NumericUpDown _numPollInterval;
    private readonly NumericUpDown _numTimeout;
    private readonly CheckBox _chkAutoStart;
    private readonly CheckBox _chkUseSessionAuth;
    private readonly RichTextBox _txtTestResult;
    private readonly Button _btnTest;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly HttpClient _http;
    private readonly AppConfig _originalConfig;

    public AppConfig Config { get; private set; }
    public bool ConfigSaved { get; private set; }
    public bool RestartRequested { get; private set; }

    public ConfigForm(AppConfig currentConfig)
    {
        Config = currentConfig;
        _originalConfig = CloneConfig(currentConfig);
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        Text = Strings.ConfigTitle;
        Icon = TrayIconGenerator.CreateApplicationIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(500, 570);
        Padding = new Padding(10);

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 11,
            Padding = new Padding(10)
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Service Name
        mainPanel.Controls.Add(new Label { Text = Strings.ConfigServiceName, Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _txtServiceName = new TextBox { Dock = DockStyle.Fill, Text = currentConfig.ServiceName };
        mainPanel.Controls.Add(_txtServiceName, 1, 0);

        // Gateway URL
        mainPanel.Controls.Add(new Label { Text = Strings.ConfigGatewayUrl, Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _txtGatewayUrl = new TextBox { Dock = DockStyle.Fill, Text = currentConfig.GatewayBaseUrl };
        mainPanel.Controls.Add(_txtGatewayUrl, 1, 1);

        // Poll Interval
        mainPanel.Controls.Add(new Label { Text = Strings.ConfigPollInterval, Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
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
        mainPanel.Controls.Add(new Label { Text = Strings.ConfigHttpTimeout, Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        _numTimeout = new NumericUpDown
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = 30,
            Value = Math.Clamp(currentConfig.HttpTimeoutSeconds, 1, 30)
        };
        mainPanel.Controls.Add(_numTimeout, 1, 3);

        // Username (optional)
        mainPanel.Controls.Add(new Label { Text = Strings.ConfigUsername, Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        _txtUsername = new TextBox { Dock = DockStyle.Fill, Text = currentConfig.Username ?? "", PlaceholderText = Strings.ConfigOptional };
        mainPanel.Controls.Add(_txtUsername, 1, 4);

        // Password (optional)
        mainPanel.Controls.Add(new Label { Text = Strings.ConfigPassword, Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
        _txtPassword = new TextBox { Dock = DockStyle.Fill, Text = currentConfig.Password ?? "", PlaceholderText = Strings.ConfigOptional, UseSystemPasswordChar = true };
        mainPanel.Controls.Add(_txtPassword, 1, 5);

        // Auto-start checkbox
        _chkAutoStart = new CheckBox
        {
            Text = Strings.ConfigAutoStart,
            Dock = DockStyle.Fill,
            Checked = IsAutoStartEnabled(),
            AutoSize = true
        };
        mainPanel.Controls.Add(_chkAutoStart, 0, 6);
        mainPanel.SetColumnSpan(_chkAutoStart, 2);

        // Use Session Auth checkbox
        _chkUseSessionAuth = new CheckBox
        {
            Text = Strings.ConfigUseSessionAuth,
            Dock = DockStyle.Fill,
            Checked = currentConfig.UseSessionAuth,
            AutoSize = true
        };
        var sessionAuthTooltip = new ToolTip();
        sessionAuthTooltip.SetToolTip(_chkUseSessionAuth, Strings.ConfigUseSessionAuthTooltip);
        mainPanel.Controls.Add(_chkUseSessionAuth, 0, 7);
        mainPanel.SetColumnSpan(_chkUseSessionAuth, 2);

        // Test Button
        _btnTest = new Button
        {
            Text = Strings.ConfigTestConnection,
            Dock = DockStyle.Fill,
            Height = 35
        };
        _btnTest.Click += OnTestButtonClick;
        var testPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 5) };
        testPanel.Controls.Add(_btnTest);
        mainPanel.Controls.Add(testPanel, 0, 8);
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
        mainPanel.Controls.Add(_txtTestResult, 0, 9);
        mainPanel.SetColumnSpan(_txtTestResult, 2);
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 0 - Service Name
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 1 - Gateway URL
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 2 - Poll Interval
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 3 - Timeout
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 4 - Username
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 5 - Password
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 6 - Auto-start checkbox
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Row 7 - Session Auth checkbox
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Row 8 - Test button
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Row 9 - Test result
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Row 10 - Buttons

        // Buttons Panel
        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 5, 0, 0)
        };

        _btnCancel = new Button { Text = Strings.ConfigCancel, Width = 80, Height = 30 };
        _btnCancel.Click += (_, _) => { ConfigSaved = false; Close(); };

        _btnSave = new Button { Text = Strings.ConfigSave, Width = 80, Height = 30 };
        _btnSave.Click += (_, _) => SaveConfig();

        buttonsPanel.Controls.Add(_btnCancel);
        buttonsPanel.Controls.Add(_btnSave);

        mainPanel.Controls.Add(buttonsPanel, 0, 10);
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
        _chkUseSessionAuth.TabIndex = 7;
        _btnTest.TabIndex = 8;
        _btnSave.TabIndex = 9;
        _btnCancel.TabIndex = 10;
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = StartupRegistryRoot.OpenSubKey(StartupRegistryKey, false);
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
            using var key = StartupRegistryRoot.OpenSubKey(StartupRegistryKey, true);
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
            AppendResult($"\n{Strings.TestUnexpectedError} {ex.Message}", Color.OrangeRed);
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
            AppendResult($"{Strings.TestConnectionTo} {baseUrl}");
            AppendResult(new string('-', 40));

            // Test 1: StatusPing
            AppendResult($"\n{Strings.TestStatusPing}");
            var (pingOk, pingInfo) = await TestEndpointAsync(baseUrl, "StatusPing");
            if (pingOk)
            {
                AppendResult(Strings.TestStatusPingOK, Color.LightGreen);
                AppendResult($"{Strings.TestResponse} {pingInfo}");
            }
            else
            {
                AppendResult($"{Strings.TestStatusPingFailed} {pingInfo}", Color.OrangeRed);
            }

            // Test 2: gwinfo
            AppendResult($"\n{Strings.TestGwinfo}");
            var (gwinfoOk, gwinfoInfo) = await TestEndpointAsync(baseUrl, "system/gwinfo");
            if (gwinfoOk)
            {
                AppendResult(Strings.TestGwinfoOK, Color.LightGreen);
                AppendResult($"{Strings.TestResponse} {TruncateResponse(gwinfoInfo, 200)}");
            }
            else
            {
                AppendResult($"{Strings.TestGwinfoFailed} {gwinfoInfo}", Color.OrangeRed);
            }

            // Test 3: Service
            AppendResult($"\n{Strings.TestService} {_txtServiceName.Text} ...");
            var (svcOk, svcInfo) = TestServiceStatus(_txtServiceName.Text);
            if (svcOk)
            {
                AppendResult($"{Strings.TestServiceOK} {svcInfo}", Color.LightGreen);
            }
            else
            {
                AppendResult($"{Strings.TestServiceFailed} {svcInfo}", Color.OrangeRed);
            }

            // Test 4: Performance Metrics (only if session auth is enabled)
            var perfOk = false;
            if (_chkUseSessionAuth.Checked)
            {
                AppendResult($"\n{Strings.TestPerformance}");

                if (string.IsNullOrWhiteSpace(_txtUsername.Text) || string.IsNullOrWhiteSpace(_txtPassword.Text))
                {
                    AppendResult(Strings.TestPerformanceSkipped, Color.Yellow);
                }
                else
                {
                    var (loginOk, perfInfo) = await TestSessionAuthAsync(baseUrl, _txtUsername.Text, _txtPassword.Text);
                    if (loginOk)
                    {
                        AppendResult(Strings.TestSessionAuthOK, Color.LightGreen);
                        AppendResult($"    {perfInfo}");
                        perfOk = true;
                    }
                    else
                    {
                        AppendResult($"{Strings.TestSessionAuthFailed} {perfInfo}", Color.OrangeRed);
                    }
                }
            }

            // Summary
            AppendResult("\n" + new string('=', 40));
            var allOk = pingOk && svcOk && (!_chkUseSessionAuth.Checked || perfOk || string.IsNullOrWhiteSpace(_txtUsername.Text));
            if (allOk)
            {
                AppendResult(Strings.TestAllPassed, Color.LightGreen);
            }
            else
            {
                AppendResult(Strings.TestSomeFailed, Color.Yellow);
            }
        }
        catch (Exception ex)
        {
            AppendResult($"\n{Strings.TestError} {ex.Message}", Color.OrangeRed);
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

    private async Task<(bool ok, string info)> TestSessionAuthAsync(string baseUrl, string username, string password)
    {
        try
        {
            // Create a new HttpClient with CookieContainer for session auth
            using var handler = new HttpClientHandler
            {
                CookieContainer = new System.Net.CookieContainer(),
                UseCookies = true
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

            var baseUri = new Uri(baseUrl.TrimEnd('/') + "/");

            // Step 1: Try multiple login endpoints
            string[] loginEndpoints = { "data/app/login", "web/login", "data/login", "login" };
            bool loginSuccess = false;
            string loginError = "";

            foreach (var endpoint in loginEndpoints)
            {
                try
                {
                    var loginUrl = new Uri(baseUri, endpoint);
                    var loginContent = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("username", username),
                        new KeyValuePair<string, string>("password", password)
                    });

                    using var loginResp = await client.PostAsync(loginUrl, loginContent);

                    if (loginResp.IsSuccessStatusCode || 
                        loginResp.StatusCode == System.Net.HttpStatusCode.Found ||
                        loginResp.StatusCode == System.Net.HttpStatusCode.SeeOther)
                    {
                        loginSuccess = true;
                        break;
                    }

                    // 401/403 means credentials wrong
                    if (loginResp.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        loginResp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        loginError = $"{(int)loginResp.StatusCode} {loginResp.ReasonPhrase}";
                        break;
                    }

                    // 404 = endpoint doesn't exist, try next
                    loginError = $"{endpoint}: {(int)loginResp.StatusCode}";
                }
                catch (Exception ex)
                {
                    loginError = ex.Message;
                }
            }

            if (!loginSuccess)
            {
                return (false, $"{Strings.TestLoginFailed} {loginError}");
            }

            // Step 2: Get performance metrics
            var perfUrl = new Uri(baseUri, "data/api/v1/systemPerformance/currentGauges");
            using var perfResp = await client.GetAsync(perfUrl);

            // If session auth didn't work, try Basic Auth as fallback
            if (!perfResp.IsSuccessStatusCode && !loginSuccess)
            {
                return await TestBasicAuthAsync(baseUrl, username, password);
            }

            if (!perfResp.IsSuccessStatusCode)
            {
                // Try Basic Auth as fallback
                return await TestBasicAuthAsync(baseUrl, username, password);
            }

            var json = await perfResp.Content.ReadAsStringAsync();

            // Parse the response
            return ParsePerformanceResponse(json);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(bool ok, string info)> TestBasicAuthAsync(string baseUrl, string username, string password)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            var baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
            var perfUrl = new Uri(baseUri, "data/api/v1/systemPerformance/currentGauges");

            using var perfResp = await client.GetAsync(perfUrl);

            if (perfResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // 401 on both form login and basic auth = likely OIDC
                return (false, $"{Strings.TestAuthAllMethodsFailed}\n    {Strings.TestAuthCheckCredentials}");
            }

            if (perfResp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // 403 = endpoint exists but access denied
                return (false, $"{Strings.TestAuthOidcRequired}");
            }

            if (!perfResp.IsSuccessStatusCode)
            {
                return (false, $"{Strings.TestPerfApiFailed} {(int)perfResp.StatusCode} {perfResp.ReasonPhrase}");
            }

            var json = await perfResp.Content.ReadAsStringAsync();
            return ParsePerformanceResponse(json, " (Basic Auth)");
        }
        catch (Exception ex)
        {
            return (false, $"Basic Auth: {ex.Message}");
        }
    }

    private static (bool ok, string info) ParsePerformanceResponse(string json, string suffix = "")
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var cpu = root.TryGetProperty("cpu", out var cpuEl) ? cpuEl.GetDouble() : 0;
            var heapMemory = root.TryGetProperty("heapMemory", out var heapEl) ? heapEl.GetInt64() : 0;
            var maxMemory = root.TryGetProperty("maxMemory", out var maxEl) ? maxEl.GetInt64() : 0;

            var heapMB = heapMemory / 1024 / 1024;
            var maxMB = maxMemory / 1024 / 1024;
            var heapPercent = maxMemory > 0 ? Math.Round((double)heapMemory / maxMemory * 100, 1) : 0;

            return (true, $"CPU: {cpu:F1}% | Heap: {heapPercent}% ({heapMB} MB / {maxMB} MB){suffix}");
        }
        catch
        {
            return (true, $"{Strings.TestRawResponse} {TruncateResponse(json, 100)}{suffix}");
        }
    }

    private static (bool ok, string info) TestServiceStatus(string serviceName)
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
            MessageBox.Show(Strings.ValidationServiceNameRequired, Strings.ValidationError, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtGatewayUrl.Text))
        {
            MessageBox.Show(Strings.ValidationGatewayUrlRequired, Strings.ValidationError, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!Uri.TryCreate(_txtGatewayUrl.Text, UriKind.Absolute, out _))
        {
            MessageBox.Show(Strings.ValidationGatewayUrlInvalid, Strings.ValidationError, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Validate session auth requires credentials
        if (_chkUseSessionAuth.Checked && (string.IsNullOrWhiteSpace(_txtUsername.Text) || string.IsNullOrWhiteSpace(_txtPassword.Text)))
        {
            MessageBox.Show(Strings.ValidationSessionAuthCredentials, Strings.ValidationError, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Config = new AppConfig
        {
            ServiceName = _txtServiceName.Text.Trim(),
            GatewayBaseUrl = _txtGatewayUrl.Text.Trim(),
            PollIntervalMs = (int)_numPollInterval.Value,
            HttpTimeoutSeconds = (int)_numTimeout.Value,
            Username = string.IsNullOrWhiteSpace(_txtUsername.Text) ? null : _txtUsername.Text.Trim(),
            Password = string.IsNullOrWhiteSpace(_txtPassword.Text) ? null : _txtPassword.Text,
            UseSessionAuth = _chkUseSessionAuth.Checked
        };

        // Save to appsettings.json with encrypted password
        try
        {
            // Create a copy for saving with encrypted password
            var configToSave = new
            {
                Gateway = new
                {
                    Config.ServiceName,
                    Config.GatewayBaseUrl,
                    Config.PollIntervalMs,
                    Config.HttpTimeoutSeconds,
                    Config.Username,
                    Password = PasswordProtection.Encrypt(Config.Password),  // Encrypt password for storage
                    Config.UseSessionAuth
                }
            };

            var json = JsonSerializer.Serialize(configToSave, new JsonSerializerOptions { WriteIndented = true });
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            System.IO.File.WriteAllText(path, json);

            // Save auto-start setting to registry
            SetAutoStart(_chkAutoStart.Checked);

            ConfigSaved = true;

            // Check if settings that require restart have changed
            var needsRestart = HasConfigChanged();

            var autoStartMsg = _chkAutoStart.Checked 
                ? Strings.SaveAutoStartEnabled 
                : Strings.SaveAutoStartDisabled;

            var authMsg = Config.HasCredentials
                ? Strings.SaveAuthConfigured
                : "";

            var sessionAuthMsg = Config.UseSessionAuth
                ? Strings.SaveSessionAuthEnabled
                : "";

            if (needsRestart)
            {
                var result = MessageBox.Show(
                    $"{Strings.SaveSuccess}{autoStartMsg}{authMsg}{sessionAuthMsg}\n\n{Strings.RestartMessage}",
                    Strings.RestartRequired,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    RestartRequested = true;
                }
            }
            else
            {
                MessageBox.Show($"{Strings.SaveSuccess}{autoStartMsg}{authMsg}{sessionAuthMsg}", Strings.Success, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Strings.SaveErrorMessage, ex.Message), Strings.SaveError, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool HasConfigChanged()
    {
        return _originalConfig.ServiceName != Config.ServiceName ||
               _originalConfig.GatewayBaseUrl != Config.GatewayBaseUrl ||
               _originalConfig.PollIntervalMs != Config.PollIntervalMs ||
               _originalConfig.HttpTimeoutSeconds != Config.HttpTimeoutSeconds ||
               _originalConfig.Username != Config.Username ||
               _originalConfig.Password != Config.Password ||
               _originalConfig.UseSessionAuth != Config.UseSessionAuth;
    }

    private static AppConfig CloneConfig(AppConfig config)
    {
        return new AppConfig
        {
            ServiceName = config.ServiceName,
            GatewayBaseUrl = config.GatewayBaseUrl,
            PollIntervalMs = config.PollIntervalMs,
            HttpTimeoutSeconds = config.HttpTimeoutSeconds,
            Username = config.Username,
            Password = config.Password,
            UseSessionAuth = config.UseSessionAuth
        };
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
