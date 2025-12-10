using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Core.Strategies;

namespace MillenniumLoadBalancer.App.Infrastructure.Factories;

internal class LoadBalancingStrategyFactory : ILoadBalancingStrategyFactory
{
    public ILoadBalancingStrategy Create(string strategyName)
    {
        if (string.IsNullOrWhiteSpace(strategyName))
        {
            throw new ArgumentException("Strategy name cannot be null or empty", nameof(strategyName));
        }

        return strategyName.ToLowerInvariant() switch
        {
            "roundrobin" or "round-robin" => new RoundRobinStrategy(),
            "random" => new RandomStrategy(),
            "fallback" => new FallbackStrategy(),
            _ => throw new ArgumentException($"Unknown load balancing strategy: {strategyName}. Supported strategies: RoundRobin, Random, Fallback", nameof(strategyName))
        };
    }
}
