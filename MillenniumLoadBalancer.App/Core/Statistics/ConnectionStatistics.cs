namespace MillenniumLoadBalancer.App.Core.Statistics;

/// <summary>
/// Connection statistics snapshot.
/// </summary>
public class ConnectionStatistics
{
    public Dictionary<string, LoadBalancerStats> LoadBalancers { get; set; } = new();
    public long TotalConnectionsAccepted { get; set; }
    public long TotalConnectionsForwarded { get; set; }
    public long TotalConnectionsFailed { get; set; }
    public long TotalActiveConnections { get; set; }
}

