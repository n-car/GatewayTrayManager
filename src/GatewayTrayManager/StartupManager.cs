using Microsoft.Win32;

namespace GatewayTrayManager;

internal static class StartupManager
{
    private const string AppName = "Gateway Tray Manager";
    private const string ExeName = "GatewayTrayManager.exe";
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsAutoStartEnabled()
    {
        foreach (var view in GetRegistryViews())
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = root.OpenSubKey(StartupRegistryKey, false);
                if (key?.GetValue(AppName) is string value && !string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }
            }
            catch
            {
                // Ignore read errors and keep checking other registry views.
            }
        }

        return false;
    }

    public static void SetAutoStart(bool enabled)
    {
        var primaryView = GetPrimaryRegistryView();

        using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, primaryView))
        using (var key = root.CreateSubKey(StartupRegistryKey, true)
                         ?? throw new InvalidOperationException("Unable to open the Windows startup registry key."))
        {
            if (enabled)
            {
                key.SetValue(AppName, $"\"{GetExecutablePath()}\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }

        // Remove stale entries written by older 32-bit installer runs to avoid duplicates.
        foreach (var view in GetRegistryViews().Where(view => view != primaryView))
        {
            DeleteAutoStartValue(view);
        }
    }

    public static string GetExecutablePath()
    {
        var exePath = Environment.ProcessPath;
        return string.IsNullOrEmpty(exePath)
            ? Path.Combine(AppContext.BaseDirectory, ExeName)
            : exePath;
    }

    private static RegistryView GetPrimaryRegistryView()
    {
        return Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
    }

    private static IEnumerable<RegistryView> GetRegistryViews()
    {
        if (Environment.Is64BitOperatingSystem)
        {
            yield return RegistryView.Registry64;
            yield return RegistryView.Registry32;
            yield break;
        }

        yield return RegistryView.Registry32;
    }

    private static void DeleteAutoStartValue(RegistryView view)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = root.OpenSubKey(StartupRegistryKey, true);
            key?.DeleteValue(AppName, false);
        }
        catch
        {
            // Best-effort cleanup of legacy registry entries.
        }
    }
}
