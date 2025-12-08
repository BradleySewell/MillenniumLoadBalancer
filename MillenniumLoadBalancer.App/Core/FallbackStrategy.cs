using MillenniumLoadBalancer.App.Core.Interfaces;

namespace MillenniumLoadBalancer.App.Core;

internal class FallbackStrategy : ILoadBalancingStrategy
{
    public IBackendService? SelectBackend(IEnumerable<IBackendService> backends)
    {
        var healthyBackends = backends.Where(b => b.IsHealthy).ToList();
        
        if (healthyBackends.Count == 0)
        {
            return null;
        }

        return healthyBackends[0];
    }
}
