using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ServiceManager;

namespace GatewayTrayManager;

internal static class Program
{
    private const string MutexName = "GatewayTrayManagerSingleton";
    private const string AppName = "GatewayTrayManager";
    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "crash.log");

    [STAThread]
    static int Main(string[] args)
    {
        // Check for service control mode (elevated process)
        if (ServiceControlHelper.IsServiceControlMode(args))
        {
            return ServiceControlHelper.ExecuteFromArgs(args);
        }

        // Global exception handlers to prevent silent crashes
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (_, e) =>
        {
            LogException("ThreadException", e.Exception);
            try
            {
                MessageBox.Show($"An error occurred:\n{e.Exception.Message}\n\nDetails logged to crash.log", 
                    "Gateway Tray Manager Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException("UnhandledException", ex);
                try
                {
                    MessageBox.Show($"A fatal error occurred:\n{ex.Message}\n\nDetails logged to crash.log", 
                        "Gateway Tray Manager Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        };

        // Also catch TaskScheduler exceptions
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogException("UnobservedTaskException", e.Exception);
            e.SetObserved(); // Prevent crash
        };

        try
        {
            // Check for single instance
            using var mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                var result = MessageBox.Show(
                    "Gateway Tray Manager is already running.\n\n" +
                    "Do you want to close the existing instance and start a new one?",
                    "Application Already Running",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);

                if (result == DialogResult.Yes)
                {
                    // Try to close the existing instance
                    if (CloseExistingInstance())
                    {
                        // Wait a moment for the old instance to close
                        Thread.Sleep(1000);

                        // Try to acquire the mutex again
                        using var newMutex = new Mutex(true, MutexName, out bool acquired);
                        if (acquired)
                        {
                            StartApplication();
                            return 0;
                        }
                    }

                    MessageBox.Show(
                        "Could not close the existing instance.\nPlease close it manually and try again.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return 0;
            }

            StartApplication();
        }
        catch (Exception ex)
        {
            LogException("Main", ex);
            MessageBox.Show($"Failed to start application:\n{ex.Message}",
                "Gateway Tray Manager Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        return 0;
    }

    private static void LogException(string source, Exception ex)
    {
        try
        {
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(LogFile, logEntry);
        }
        catch { }
    }

    private static void StartApplication()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
    }

    private static bool CloseExistingInstance()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(AppName)
                .Where(p => p.Id != currentProcess.Id)
                .ToList();

            if (processes.Count == 0)
            {
                // Try with process name from executable
                var exeName = System.IO.Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? AppName);
                processes = Process.GetProcessesByName(exeName)
                    .Where(p => p.Id != currentProcess.Id)
                    .ToList();
            }

            foreach (var process in processes)
            {
                try
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(3000))
                    {
                        process.Kill();
                        process.WaitForExit(2000);
                    }
                }
                catch
                {
                    // Ignore errors closing individual processes
                }
                finally
                {
                    process.Dispose();
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
