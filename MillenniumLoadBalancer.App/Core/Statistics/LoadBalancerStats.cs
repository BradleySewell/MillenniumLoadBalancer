namespace MillenniumLoadBalancer.App.Core.Statistics;

/// <summary>
/// Statistics for a single load balancer.
/// </summary>
public class LoadBalancerStats
{
    public string Name { get; set; } = string.Empty;
    public long ConnectionsAccepted { get; set; }
    public long ConnectionsForwarded { get; set; }
    public long ConnectionsFailed { get; set; }
    public long ActiveConnections { get; set; }
    public Dictionary<string, BackendStats> Backends { get; set; } = new();
}

