using MillenniumLoadBalancer.App.Core.Configuration;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class BackendConfigurationTests
{
    [TestMethod]
    public void Address_CanBeSetAndRetrieved()
    {

        var config = new BackendConfiguration
        {
            Address = "127.0.0.1"
        };

        
        Assert.AreEqual("127.0.0.1", config.Address);
    }

    [TestMethod]
    public void Port_CanBeSetAndRetrieved()
    {

        var config = new BackendConfiguration
        {
            Port = 8080
        };

        
        Assert.AreEqual(8080, config.Port);
    }

    [TestMethod]
    public void Address_DefaultsToEmptyString()
    {

        var config = new BackendConfiguration();

        
        Assert.AreEqual(string.Empty, config.Address);
    }

    [TestMethod]
    public void Port_DefaultsToZero()
    {

        var config = new BackendConfiguration();

        
        Assert.AreEqual(0, config.Port);
    }
}

[TestClass]
public sealed class ListenerConfigurationTests
{
    [TestMethod]
    public void Name_CanBeSetAndRetrieved()
    {

        var config = new ListenerConfiguration
        {
            Name = "TestListener"
        };

        
        Assert.AreEqual("TestListener", config.Name);
    }

    [TestMethod]
    public void Protocol_DefaultsToTcp()
    {

        var config = new ListenerConfiguration();

        
        Assert.AreEqual("TCP", config.Protocol);
    }

    [TestMethod]
    public void Strategy_DefaultsToRoundRobin()
    {

        var config = new ListenerConfiguration();

        
        Assert.AreEqual("RoundRobin", config.Strategy);
    }

    [TestMethod]
    public void ListenAddress_CanBeSetAndRetrieved()
    {

        var config = new ListenerConfiguration
        {
            ListenAddress = "0.0.0.0"
        };

        
        Assert.AreEqual("0.0.0.0", config.ListenAddress);
    }

    [TestMethod]
    public void ListenPort_CanBeSetAndRetrieved()
    {

        var config = new ListenerConfiguration
        {
            ListenPort = 8080
        };

        
        Assert.AreEqual(8080, config.ListenPort);
    }

    [TestMethod]
    public void Backends_IsInitialized()
    {

        var config = new ListenerConfiguration();

        
        Assert.IsNotNull(config.Backends);
        Assert.IsEmpty(config.Backends);
    }

    [TestMethod]
    public void Backends_CanBeSet()
    {

        var config = new ListenerConfiguration
        {
            Backends = new List<BackendConfiguration>
            {
                new() { Address = "127.0.0.1", Port = 8080 }
            }
        };

        
        Assert.IsNotNull(config.Backends);
        Assert.HasCount(1, config.Backends);
        Assert.AreEqual("127.0.0.1", config.Backends[0].Address);
        Assert.AreEqual(8080, config.Backends[0].Port);
    }

    [TestMethod]
    public void RecoveryCheckIntervalSeconds_DefaultsToTen()
    {

        var config = new ListenerConfiguration();

        
        Assert.AreEqual(10, config.RecoveryCheckIntervalSeconds);
    }

    [TestMethod]
    public void RecoveryDelaySeconds_DefaultsToThirty()
    {

        var config = new ListenerConfiguration();

        
        Assert.AreEqual(30, config.RecoveryDelaySeconds);
    }

    [TestMethod]
    public void ConnectionTimeoutSeconds_DefaultsToTen()
    {

        var config = new ListenerConfiguration();

        
        Assert.AreEqual(10, config.ConnectionTimeoutSeconds);
    }

    [TestMethod]
    public void SendTimeoutSeconds_DefaultsToThirty()
    {

        var config = new ListenerConfiguration();

        
        Assert.AreEqual(30, config.SendTimeoutSeconds);
    }

    [TestMethod]
    public void ReceiveTimeoutSeconds_DefaultsToThirty()
    {

        var config = new ListenerConfiguration();

        
        Assert.AreEqual(30, config.ReceiveTimeoutSeconds);
    }
}

[TestClass]
public sealed class LoadBalancerOptionsTests
{
    [TestMethod]
    public void Listeners_IsInitialized()
    {

        var options = new LoadBalancerOptions();

        
        Assert.IsNotNull(options.Listeners);
        Assert.IsEmpty(options.Listeners);
    }

    [TestMethod]
    public void Listeners_CanBeSet()
    {

        var options = new LoadBalancerOptions
        {
            Listeners = new List<ListenerConfiguration>
            {
                new() { Name = "LB1", ListenAddress = "127.0.0.1", ListenPort = 8080 }
            }
        };

        
        Assert.IsNotNull(options.Listeners);
        Assert.HasCount(1, options.Listeners);
        Assert.AreEqual("LB1", options.Listeners[0].Name);
    }
}
