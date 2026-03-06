using System.Diagnostics;
using System.Security.Principal;

namespace ServiceManager;

/// <summary>
/// Helper class for UAC elevation and admin privilege detection.
/// </summary>
public static class ElevationHelper
{
    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs a command with elevated (admin) privileges using UAC prompt.
    /// </summary>
    /// <param name="executable">Path to the executable</param>
    /// <param name="arguments">Command line arguments</param>
    /// <param name="waitForExit">Whether to wait for the process to complete</param>
    /// <param name="timeoutMs">Timeout in milliseconds when waiting</param>
    /// <returns>True if the elevated process completed successfully</returns>
    public static bool RunElevated(string executable, string arguments, bool waitForExit = true, int timeoutMs = 60000)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas", // This triggers UAC prompt
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            if (waitForExit)
            {
                var completed = process.WaitForExit(timeoutMs);
                return completed && process.ExitCode == 0;
            }

            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED - User declined UAC prompt
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs a service control operation with elevation if needed.
    /// Uses the current executable with --service-control argument.
    /// </summary>
    /// <param name="action">The service action: start, stop, or restart</param>
    /// <param name="serviceName">The name of the Windows service</param>
    /// <returns>True if the operation was successful</returns>
    public static bool RunServiceControlElevated(string action, string serviceName)
    {
        var currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(currentExe)) return false;

        var arguments = $"--service-control {action} \"{serviceName}\"";
        return RunElevated(currentExe, arguments, waitForExit: true, timeoutMs: 120000);
    }
}
