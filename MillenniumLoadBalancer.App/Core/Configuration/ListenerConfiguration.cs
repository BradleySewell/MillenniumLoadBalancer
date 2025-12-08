namespace MillenniumLoadBalancer.App.Core.Configuration;

public class ListenerConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Protocol { get; set; } = "TCP";
    public string Strategy { get; set; } = "RoundRobin";
    public string ListenAddress { get; set; } = string.Empty;
    public int ListenPort { get; set; }
    public List<BackendConfiguration> Backends { get; set; } = new();
    public int RecoveryCheckIntervalSeconds { get; set; } = 10;
    public int RecoveryDelaySeconds { get; set; } = 30;
    public int ConnectionTimeoutSeconds { get; set; } = 10;
    public int SendTimeoutSeconds { get; set; } = 30;
    public int ReceiveTimeoutSeconds { get; set; } = 30;
}

