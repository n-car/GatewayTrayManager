using System.ServiceProcess;

namespace ServiceManager;

/// <summary>
/// Base class for monitoring Windows services.
/// </summary>
public class ServiceMonitor : IDisposable
{
    protected readonly string ServiceName;

    public ServiceMonitor(string serviceName)
    {
        ServiceName = serviceName;
    }

    /// <summary>
    /// Gets a snapshot of the current service status.
    /// </summary>
    public virtual Task<ServiceSnapshot> GetSnapshotAsync()
    {
        var status = GetServiceStatusSafe(ServiceName);
        return Task.FromResult(new ServiceSnapshot(status, true, status.ToString()));
    }

    public Task StartServiceAsync() => ChangeServiceAsync(ServiceAction.Start);
    public Task StopServiceAsync() => ChangeServiceAsync(ServiceAction.Stop);
    public Task RestartServiceAsync() => ChangeServiceAsync(ServiceAction.Restart);

    protected virtual async Task ChangeServiceAsync(ServiceAction action)
    {
        using var sc = new ServiceController(ServiceName);
        sc.Refresh();

        if (action == ServiceAction.Start)
        {
            if (sc.Status == ServiceControllerStatus.Running) return;
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(90));
            return;
        }

        if (action == ServiceAction.Stop)
        {
            if (sc.Status == ServiceControllerStatus.Stopped) return;
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(90));
            return;
        }

        // Restart
        if (sc.Status != ServiceControllerStatus.Stopped)
        {
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(90));
        }

        await Task.Delay(1500);

        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(90));
    }

    protected static ServiceControllerStatus GetServiceStatusSafe(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            sc.Refresh();
            return sc.Status;
        }
        catch
        {
            return ServiceControllerStatus.Stopped;
        }
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    protected enum ServiceAction { Start, Stop, Restart }
}
