using System.ServiceProcess;

namespace ServiceManager;

/// <summary>
/// Helper class for executing service control operations.
/// Handles command-line arguments for elevated service control.
/// </summary>
public static class ServiceControlHelper
{
    /// <summary>
    /// Command line argument prefix for service control operations.
    /// </summary>
    public const string ServiceControlArg = "--service-control";

    /// <summary>
    /// Checks if the application was started with service control arguments.
    /// </summary>
    public static bool IsServiceControlMode(string[] args)
    {
        return args.Length >= 3 && args[0] == ServiceControlArg;
    }

    /// <summary>
    /// Executes a service control operation from command line arguments.
    /// Call this from Main() when IsServiceControlMode returns true.
    /// </summary>
    /// <param name="args">Command line arguments: --service-control {action} {serviceName}</param>
    /// <returns>Exit code: 0 = success, 1 = error</returns>
    public static int ExecuteFromArgs(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: --service-control <start|stop|restart> <serviceName>");
            return 1;
        }

        var action = args[1].ToLowerInvariant();
        var serviceName = args[2];

        try
        {
            return ExecuteServiceControl(action, serviceName) ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Service control error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Executes a service control operation directly.
    /// Requires administrator privileges.
    /// </summary>
    /// <param name="action">Action: start, stop, or restart</param>
    /// <param name="serviceName">Windows service name</param>
    /// <returns>True if successful</returns>
    public static bool ExecuteServiceControl(string action, string serviceName)
    {
        using var sc = new ServiceController(serviceName);
        sc.Refresh();

        var timeout = TimeSpan.FromSeconds(90);

        switch (action)
        {
            case "start":
                if (sc.Status == ServiceControllerStatus.Running)
                    return true;
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
                return sc.Status == ServiceControllerStatus.Running;

            case "stop":
                if (sc.Status == ServiceControllerStatus.Stopped)
                    return true;
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                return sc.Status == ServiceControllerStatus.Stopped;

            case "restart":
                // Stop first if running
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                }
                
                // Brief pause before restart
                Thread.Sleep(1500);
                
                // Start
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, timeout);
                return sc.Status == ServiceControllerStatus.Running;

            default:
                throw new ArgumentException($"Unknown action: {action}");
        }
    }

    /// <summary>
    /// Executes a service control operation, using UAC elevation if not running as admin.
    /// </summary>
    /// <param name="action">Action: start, stop, or restart</param>
    /// <param name="serviceName">Windows service name</param>
    /// <returns>True if successful</returns>
    public static bool ExecuteWithElevation(string action, string serviceName)
    {
        // If already admin, execute directly
        if (ElevationHelper.IsRunningAsAdmin())
        {
            return ExecuteServiceControl(action, serviceName);
        }

        // Otherwise, launch elevated process
        return ElevationHelper.RunServiceControlElevated(action, serviceName);
    }
}
