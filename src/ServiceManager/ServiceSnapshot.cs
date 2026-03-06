using System.ServiceProcess;

namespace ServiceManager;

/// <summary>
/// Represents a snapshot of a service's status.
/// </summary>
public record ServiceSnapshot(
    ServiceControllerStatus ServiceStatus,
    bool IsHealthy,
    string StatusInfo)
{
    public bool CanStart => ServiceStatus is ServiceControllerStatus.Stopped;
    public bool CanStop => ServiceStatus is ServiceControllerStatus.Running;
    public bool CanRestart => ServiceStatus is ServiceControllerStatus.Running;
}
