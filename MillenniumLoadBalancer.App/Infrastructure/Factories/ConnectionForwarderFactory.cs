using MillenniumLoadBalancer.App.Core.Interfaces;

namespace MillenniumLoadBalancer.App.Infrastructure.Factories;

internal class ConnectionForwarderFactory : IConnectionForwarderFactory
{
    public IConnectionForwarder Create(string protocol, int connectionTimeoutSeconds, int sendTimeoutSeconds, int receiveTimeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            throw new NotSupportedException($"Protocol '{protocol}' is not supported. Supported protocols: TCP");
        }

        return protocol.ToUpperInvariant() switch
        {
            "TCP" => new TcpConnectionForwarder(connectionTimeoutSeconds, sendTimeoutSeconds, receiveTimeoutSeconds),
            _ => throw new NotSupportedException($"Protocol '{protocol}' is not supported. Supported protocols: TCP")
        };
    }
}

