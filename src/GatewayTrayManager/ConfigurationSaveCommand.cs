using System.Diagnostics;
using System.Text.Json;
using GatewayTrayManager.Localization;
using ServiceManager;

namespace GatewayTrayManager;

internal static class ConfigurationSaveCommand
{
    private const string ConfigFileName = "appsettings.json";
    public const string SaveConfigArg = "--save-config";

    public static bool IsSaveConfigMode(string[] args)
    {
        return args.Length >= 3 && args[0].Equals(SaveConfigArg, StringComparison.OrdinalIgnoreCase);
    }

    public static int ExecuteFromArgs(string[] args)
    {
        if (args.Length < 3 || !bool.TryParse(args[2], out var autoStartEnabled))
        {
            return 1;
        }

        try
        {
            SaveFromJsonFile(args[1], autoStartEnabled);
            return 0;
        }
        catch (Exception ex)
        {
            LogCommandError(ex);
            return 1;
        }
    }

    public static void Save(string json, bool autoStartEnabled)
    {
        if (ElevationHelper.IsRunningAsAdmin())
        {
            SaveJsonContent(json, autoStartEnabled);
            return;
        }

        SaveWithElevation(json, autoStartEnabled);
    }

    private static void SaveWithElevation(string json, bool autoStartEnabled)
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"GatewayTrayManager.{Guid.NewGuid():N}.{ConfigFileName}");

        File.WriteAllText(tempPath, json);

        try
        {
            var arguments = $"{SaveConfigArg} {QuoteArgument(tempPath)} {autoStartEnabled}";
            if (!ElevationHelper.RunElevated(StartupManager.GetExecutablePath(), arguments, waitForExit: true, timeoutMs: 60000))
            {
                throw new InvalidOperationException(Strings.SaveElevatedFailed);
            }
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void SaveFromJsonFile(string sourceConfigPath, bool autoStartEnabled)
    {
        var json = File.ReadAllText(sourceConfigPath);
        SaveJsonContent(json, autoStartEnabled);
    }

    private static void SaveJsonContent(string json, bool autoStartEnabled)
    {
        using (JsonDocument.Parse(json))
        {
            // Validate before overwriting the installed configuration file.
        }

        StartupManager.SetAutoStart(autoStartEnabled);

        var targetPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        File.WriteAllText(targetPath, json);
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Temporary config payload cleanup is best-effort.
        }
    }

    private static void LogCommandError(Exception ex)
    {
        try
        {
            var logFile = Path.Combine(AppContext.BaseDirectory, "crash.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SaveConfig: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logFile, entry);
        }
        catch
        {
            Debug.WriteLine(ex);
        }
    }
}
