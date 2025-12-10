namespace MillenniumLoadBalancer.App.Core.Configuration;

public class LoadBalancerOptions
{
    public List<ListenerConfiguration> Listeners { get; set; } = new();
    public bool EnableVisualMode { get; set; } = false;
}

