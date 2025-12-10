using MillenniumLoadBalancer.App.Core.Interfaces;

namespace MillenniumLoadBalancer.App.Core.Strategies;

internal class RoundRobinStrategy : ILoadBalancingStrategy
{
    private long _currentIndex = 0;
    private readonly object _lock = new();

    public IBackendService? SelectBackend(IEnumerable<IBackendService> backends)
    {
        var healthyBackends = backends.Where(b => b.IsHealthy).ToList();
        
        if (healthyBackends.Count == 0)
        {
            return null;
        }

        lock (_lock)
        {
            var selected = healthyBackends[(int)(_currentIndex % healthyBackends.Count)];
            _currentIndex++;
            
            return selected;
        }
    }
}

