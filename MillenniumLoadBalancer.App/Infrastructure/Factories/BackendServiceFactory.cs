using MillenniumLoadBalancer.App.Core;
using MillenniumLoadBalancer.App.Core.Interfaces;

namespace MillenniumLoadBalancer.App.Infrastructure.Factories;

internal class BackendServiceFactory : IBackendServiceFactory
{
    public IBackendService Create(string address, int port)
    {
        return new BackendService(address, port);
    }
}

