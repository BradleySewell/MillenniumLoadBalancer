using MillenniumLoadBalancer.App.Core.Interfaces;

internal class RoundRobinStrategy : ILoadBalancingStrategy
{
    private long _currentIndex = -1;

    public IBackendService? SelectBackend(IEnumerable<IBackendService> backends)
    {
        var healthyBackends = backends.Where(b => b.IsHealthy).ToList();

        if (healthyBackends.Count == 0)
            return null;

        var index = Interlocked.Increment(ref _currentIndex);
        var selectedIndex = (int)(index % healthyBackends.Count);

        if (selectedIndex < 0)
            selectedIndex += healthyBackends.Count;

        return healthyBackends[selectedIndex];
    }
}
