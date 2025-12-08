using MillenniumLoadBalancer.App.Core.Interfaces;

namespace MillenniumLoadBalancer.App.Core;

internal class RoundRobinStrategy : ILoadBalancingStrategy
{
    private int _currentIndex = 0;
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
            var selected = healthyBackends[_currentIndex % healthyBackends.Count];
            
            // Reset to 0 when reaching int.MaxValue to prevent overflow
            if (_currentIndex >= int.MaxValue)
            {
                _currentIndex = 0;
            }
            else
            {
                _currentIndex++;
            }
            
            return selected;
        }
    }
}

