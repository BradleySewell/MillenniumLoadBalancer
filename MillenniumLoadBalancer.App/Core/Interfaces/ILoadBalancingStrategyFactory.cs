namespace MillenniumLoadBalancer.App.Core.Interfaces;

public interface ILoadBalancingStrategyFactory
{
    ILoadBalancingStrategy Create(string strategyName);
}
