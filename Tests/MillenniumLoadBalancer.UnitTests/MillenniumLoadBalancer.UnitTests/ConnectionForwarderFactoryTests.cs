using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure.Factories;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class ConnectionForwarderFactoryTests
{
    [TestMethod]
    public void Create_WithTcpProtocol_ReturnsTcpConnectionForwarder()
    {
        
        var factory = new ConnectionForwarderFactory();

        
        var forwarder = factory.Create("TCP", 10, 30, 30);

        
        Assert.IsNotNull(forwarder);
        Assert.IsInstanceOfType(forwarder, typeof(IConnectionForwarder));
    }

    [TestMethod]
    public void Create_WithTcpProtocolLowerCase_ReturnsTcpConnectionForwarder()
    {
        
        var factory = new ConnectionForwarderFactory();

        
        var forwarder = factory.Create("tcp", 10, 30, 30);

        
        Assert.IsNotNull(forwarder);
        Assert.IsInstanceOfType(forwarder, typeof(IConnectionForwarder));
    }

    [TestMethod]
    public void Create_WithUnsupportedProtocol_ThrowsNotSupportedException()
    {
        
        var factory = new ConnectionForwarderFactory();

        
        var ex = Assert.ThrowsExactly<NotSupportedException>(
            () => factory.Create("UDP", 10, 30, 30));
        
        Assert.Contains("UDP", ex.Message);
        Assert.Contains("not supported", ex.Message);
    }

    [TestMethod]
    public void Create_WithNullProtocol_ThrowsNotSupportedException()
    {
        
        var factory = new ConnectionForwarderFactory();

        
        Assert.ThrowsExactly<NotSupportedException>(
            () => factory.Create(null!, 10, 30, 30));
    }

    [TestMethod]
    public void Create_WithEmptyProtocol_ThrowsNotSupportedException()
    {
        
        var factory = new ConnectionForwarderFactory();

        
        Assert.ThrowsExactly<NotSupportedException>(
            () => factory.Create("", 10, 30, 30));
    }
}
