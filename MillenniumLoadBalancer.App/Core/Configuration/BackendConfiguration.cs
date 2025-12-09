namespace MillenniumLoadBalancer.App.Core.Configuration;

public class BackendConfiguration
{
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool EnableTls { get; set; } = false;
    public bool ValidateCertificate { get; set; } = true;
}

