namespace ServiceManager;

/// <summary>
/// Base configuration for service monitoring.
/// </summary>
public class ServiceConfig
{
    public string ServiceName { get; set; } = "Service";
    public int PollIntervalMs { get; set; } = 3000;
}
