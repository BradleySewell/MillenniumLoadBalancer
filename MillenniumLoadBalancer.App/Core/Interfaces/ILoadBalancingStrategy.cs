namespace MillenniumLoadBalancer.App.Core.Interfaces;

public interface ILoadBalancingStrategy
{
    IBackendService? SelectBackend(IEnumerable<IBackendService> backends);
}

