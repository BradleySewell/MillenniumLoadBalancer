namespace MillenniumLoadBalancer.App.Core.Interfaces;

public interface IBackendServiceFactory
{
    IBackendService Create(string address, int port, bool enableTls, bool validateCertificate);
}

