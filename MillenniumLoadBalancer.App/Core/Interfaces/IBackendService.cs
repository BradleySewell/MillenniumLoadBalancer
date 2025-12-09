namespace MillenniumLoadBalancer.App.Core.Interfaces;

public interface IBackendService
{
    string Address { get; }
    int Port { get; }
    bool EnableTls { get; }
    bool ValidateCertificate { get; }
    bool IsHealthy { get; }
    DateTime? LastFailure { get; }
    void MarkUnhealthy();
    void MarkHealthy();
}

