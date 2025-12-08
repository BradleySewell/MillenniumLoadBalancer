using MillenniumLoadBalancer.App.Core.Configuration;

namespace MillenniumLoadBalancer.App.Core.Interfaces;

public interface ILoadBalancerFactory
{
    ILoadBalancer Create(ListenerConfiguration configuration);
}

