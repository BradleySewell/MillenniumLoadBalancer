using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure.Factories;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class LoadBalancingStrategyFactoryTests
{
    [TestMethod]
    public void Create_WithRoundRobin_ReturnsRoundRobinStrategy()
    {
        
        var factory = new LoadBalancingStrategyFactory();

        
        var strategy = factory.Create("RoundRobin");

        
        Assert.IsNotNull(strategy);
        Assert.IsInstanceOfType(strategy, typeof(ILoadBalancingStrategy));
    }

    [TestMethod]
    public void Create_WithRoundRobinLowerCase_ReturnsRoundRobinStrategy()
    {
        
        var factory = new LoadBalancingStrategyFactory();

        
        var strategy = factory.Create("roundrobin");

        
        Assert.IsNotNull(strategy);
        Assert.IsInstanceOfType(strategy, typeof(ILoadBalancingStrategy));
    }

    [TestMethod]
    public void Create_WithRoundRobinHyphenated_ReturnsRoundRobinStrategy()
    {
        
        var factory = new LoadBalancingStrategyFactory();

        
        var strategy = factory.Create("round-robin");

        
        Assert.IsNotNull(strategy);
        Assert.IsInstanceOfType(strategy, typeof(ILoadBalancingStrategy));
    }

    [TestMethod]
    public void Create_WithUnknownStrategy_ThrowsArgumentException()
    {
        
        var factory = new LoadBalancingStrategyFactory();

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => factory.Create("UnknownStrategy"));
        
        Assert.Contains("UnknownStrategy", ex.Message);
        Assert.IsTrue(ex.Message.Contains("not supported") || ex.Message.Contains("Unknown"));
    }

    [TestMethod]
    public void Create_WithNullStrategy_ThrowsArgumentException()
    {
        
        var factory = new LoadBalancingStrategyFactory();

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => factory.Create(null!));
        
        Assert.IsTrue(ex.Message.Contains("null") || ex.Message.Contains("empty"));
    }

    [TestMethod]
    public void Create_WithEmptyStrategy_ThrowsArgumentException()
    {
        
        var factory = new LoadBalancingStrategyFactory();

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => factory.Create(""));
        
        Assert.IsTrue(ex.Message.Contains("null") || ex.Message.Contains("empty"));
    }

    [TestMethod]
    public void Create_WithWhitespaceStrategy_ThrowsArgumentException()
    {
        
        var factory = new LoadBalancingStrategyFactory();

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => factory.Create("   "));
        
        Assert.IsTrue(ex.Message.Contains("null") || ex.Message.Contains("empty"));
    }
}
