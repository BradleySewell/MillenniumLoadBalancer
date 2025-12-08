using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure.Factories;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class BackendServiceFactoryTests
{
    [TestMethod]
    public void Create_ReturnsBackendServiceWithCorrectAddressAndPort()
    {
        
        var factory = new BackendServiceFactory();

        
        var backend = factory.Create("127.0.0.1", 8080);

        
        Assert.IsNotNull(backend);
        Assert.IsInstanceOfType(backend, typeof(IBackendService));
        Assert.AreEqual("127.0.0.1", backend.Address);
        Assert.AreEqual(8080, backend.Port);
    }

    [TestMethod]
    public void Create_ReturnsNewInstanceEachTime()
    {
        
        var factory = new BackendServiceFactory();

        
        var backend1 = factory.Create("127.0.0.1", 8080);
        var backend2 = factory.Create("127.0.0.1", 8080);

        
        Assert.AreNotSame(backend1, backend2);
    }

    [TestMethod]
    public void Create_WithDifferentAddresses_ReturnsDifferentBackends()
    {
        
        var factory = new BackendServiceFactory();

        
        var backend1 = factory.Create("127.0.0.1", 8080);
        var backend2 = factory.Create("192.168.1.1", 8080);

        
        Assert.AreNotEqual(backend1.Address, backend2.Address);
    }

    [TestMethod]
    public void Create_WithDifferentPorts_ReturnsDifferentBackends()
    {
        
        var factory = new BackendServiceFactory();

        
        var backend1 = factory.Create("127.0.0.1", 8080);
        var backend2 = factory.Create("127.0.0.1", 8081);

        
        Assert.AreNotEqual(backend1.Port, backend2.Port);
    }
}
