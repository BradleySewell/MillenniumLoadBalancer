namespace MillenniumLoadBalancer.App.Core.Interfaces;

public interface IConnectionForwarder
{
    Task<bool> ForwardAsync(Stream clientStream, IBackendService backend, CancellationToken cancellationToken = default);
}

