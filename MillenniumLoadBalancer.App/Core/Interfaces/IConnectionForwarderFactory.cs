namespace MillenniumLoadBalancer.App.Core.Interfaces;

public interface IConnectionForwarderFactory
{
    IConnectionForwarder Create(string protocol, int connectionTimeoutSeconds, int sendTimeoutSeconds, int receiveTimeoutSeconds);
}

