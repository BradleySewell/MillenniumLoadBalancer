# Millennium Load Balancer

A high-performance, production-ready TCP load balancer built with .NET 10.0. Millennium Load Balancer provides intelligent traffic distribution across multiple backend servers with health checking, automatic failover, and configurable load balancing strategies.

## Features

- **TCP Load Balancing**: Distribute TCP connections across multiple backend servers
- **HTTPS/TLS Support**: TLS passthrough support for HTTPS backends with configurable certificate validation
- **Health Checking**: Automatic health monitoring with configurable intervals and recovery delays, including TLS handshake verification for HTTPS backends
- **Load Balancing Strategies**: 
  - Round Robin (default) - Distributes connections evenly across backends
  - Random - Randomly selects from healthy backends
  - Fallback - Always uses the first healthy backend, moves to next when current fails
  - Extensible architecture for custom strategies
- **Automatic Failover**: Unhealthy backends are automatically removed from rotation and re-added when recovered
- **Multiple Listeners**: Configure multiple load balancer instances with different configurations
- **Connection Timeouts**: Configurable connection, send, and receive timeouts
- **Comprehensive Logging**: File-based logging with daily rotation and console output
- **Docker Support**: Containerized deployment with multi-stage Docker builds
- **Cross-Platform**: Runs on Windows, Linux, and macOS

## Architecture

Millennium Load Balancer follows a clean, modular architecture:

- **Core**: Core interfaces and load balancing strategies
- **Infrastructure**: Factories, connection forwarding, and service implementations
- **Configuration**: JSON-based configuration via `appsettings.json`
- **Logging**: Custom file logger with daily log rotation

### Key Components

- `ILoadBalancer`: Main load balancer interface
- `ILoadBalancingStrategy`: Strategy pattern for load balancing algorithms
- `IBackendHealthCheckService`: Health checking service for backend servers
- `IConnectionForwarder`: TCP connection forwarding implementation
- `ILoadBalancerManager`: Manages multiple load balancer instances

## Requirements

- .NET 10.0 SDK or Runtime
- Windows, Linux, or macOS
- Network access to backend servers

## Installation

### From Source

1. Clone the repository:
```bash
git clone https://github.com/bradleysewell/MillenniumLoadBalancer.git
cd MillenniumLoadBalancer
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the solution:
```bash
dotnet build --configuration Release
```

4. Run the application:
```bash
cd MillenniumLoadBalancer.App
dotnet run
```

### Using Docker

1. Build the Docker image:
```bash
docker build -t millennium-loadbalancer -f MillenniumLoadBalancer.App/Dockerfile .
```

2. Run the container:
```bash
docker run -d \
  --name loadbalancer \
  -p 8080:8080 \
  -v $(pwd)/MillenniumLoadBalancer.App/appsettings.json:/app/appsettings.json \
  millennium-loadbalancer
```

## Configuration

Configuration is done via `appsettings.json`. Here's an example configuration:

```json
{
  "LoadBalancer": {
    "Listeners": [
      {
        "Name": "Main Load Balancer",
        "Protocol": "TCP",
        "Strategy": "RoundRobin",
        "ListenAddress": "0.0.0.0",
        "ListenPort": 8080,
        "RecoveryCheckIntervalSeconds": 10,
        "RecoveryDelaySeconds": 30,
        "ConnectionTimeoutSeconds": 10,
        "SendTimeoutSeconds": 30,
        "ReceiveTimeoutSeconds": 30,
        "Backends": [
          {
            "Address": "127.0.0.1",
            "Port": 5001,
            "EnableTls": false,
            "ValidateCertificate": false
          },
          {
            "Address": "127.0.0.1",
            "Port": 5002,
            "EnableTls": false,
            "ValidateCertificate": false
          }
        ]
      }
    ]
  }
}
```

### Configuration Options

#### Listener Configuration

- **Name**: Friendly name for the load balancer instance
- **Protocol**: Currently supports "TCP"
- **Strategy**: Load balancing strategy ("RoundRobin", "Random", "Fallback", or custom)
- **ListenAddress**: IP address to bind to (use "0.0.0.0" for all interfaces)
- **ListenPort**: Port number to listen on
- **RecoveryCheckIntervalSeconds**: How often to check if unhealthy backends have recovered (default: 10)
- **RecoveryDelaySeconds**: Minimum time a backend must be healthy before being re-added (default: 30)
- **ConnectionTimeoutSeconds**: Timeout for establishing connections to backends (default: 10)
- **SendTimeoutSeconds**: Timeout for sending data (default: 30)
- **ReceiveTimeoutSeconds**: Timeout for receiving data (default: 30)

#### Backend Configuration

- **Address**: IP address or hostname of the backend server
- **Port**: Port number of the backend server
- **EnableTls**: Enable TLS/HTTPS support for this backend (default: false). When false, `ValidateCertificate` is ignored.
- **ValidateCertificate**: Whether to validate the backend's SSL certificate during health checks when `EnableTls` is true (default: true). Set to false for self-signed certificates in development. This setting has no effect when `EnableTls` is false.

### HTTPS/TLS Backends

To configure HTTPS backends, set `EnableTls` to `true` for each backend:

```json
{
  "LoadBalancer": {
    "Listeners": [
      {
        "Name": "HTTPS Load Balancer",
        "Protocol": "TCP",
        "ListenPort": 443,
        "Backends": [
          {
            "Address": "192.168.1.10",
            "Port": 443,
            "EnableTls": true,
            "ValidateCertificate": true
          },
          {
            "Address": "192.168.1.11",
            "Port": 443,
            "EnableTls": true,
            "ValidateCertificate": true
          }
        ]
      }
    ]
  }
}
```

**Important Notes:**
- The load balancer uses **TLS passthrough**, meaning the TLS handshake happens directly between the client and backend
- **No SSL/TLS certificate is required on the load balancer** - it simply forwards encrypted traffic transparently
- The **backend servers** must have their own SSL/TLS certificates configured (as they normally would for HTTPS)

**⚠️ Certificate Mismatch Consideration:**
With TLS passthrough, the client receives the **backend's certificate**, not a load balancer certificate. This means:
- **Certificate Mismatch Will Occur** if the client connects to `loadbalancer.example.com` but the backend's certificate is for `backend.example.com`
- **Certificate Mismatch Will Also Occur** if the client connects via IP address (e.g., `https://192.168.1.10:443`) but the backend's certificate only has hostnames, not IP addresses

**Solutions:**
1. **Best Practice (Domain-based)**: Configure the backend's certificate to include the load balancer's hostname in the **Subject Alternative Names (SAN)** field:
   ```
   Subject: CN=backend1.example.com
   SAN: DNS:backend1.example.com, DNS:lb.example.com
   ```

2. **Best Practice (IP-based)**: If connecting via IP address, include the IP address in the certificate's SAN:
   ```
   Subject: CN=backend1.example.com
   SAN: DNS:backend1.example.com, IP:192.168.1.10
   ```
   Note: IP addresses in certificates are less common and may have limitations with some clients.

3. **Alternative**: Clients can be configured to accept the certificate mismatch (not recommended for production)

4. **Alternative**: Use DNS to point the load balancer hostname directly to backend hostnames (defeats load balancing purpose)

**Recommendation:**
- **Use domain names** (not IP addresses) for HTTPS connections when possible
- Domain-based certificates are more flexible and widely supported
- IP addresses in certificates work but are less common and may cause issues with some clients

- For self-signed certificates in development, set `ValidateCertificate` to `false`
- Health checks will perform a TLS handshake to verify HTTPS backends are responding correctly

**Configuration Behavior:**
- **`EnableTls = true, ValidateCertificate = true`**: Health checks perform TLS handshake and validate the backend's certificate. Backend will be marked unhealthy if certificate is invalid.
- **`EnableTls = true, ValidateCertificate = false`**: Health checks perform TLS handshake but accept any certificate (including self-signed or invalid ones). Useful for development/testing.
- **`EnableTls = false`**: Health checks only verify TCP connectivity (no TLS handshake). `ValidateCertificate` setting is ignored. The load balancer still forwards all traffic transparently, so if a client connects with HTTPS, the TLS handshake will still work, but health checks won't verify TLS functionality.

**Note:** `ValidateCertificate` only affects **health checks**, not the actual client connections. When clients connect through the load balancer, they will validate the backend's certificate according to their own settings (browsers/clients validate certificates by default).

### Multiple Listeners

You can configure multiple load balancer instances by adding additional entries to the `Listeners` array:

```json
{
  "LoadBalancer": {
    "Listeners": [
      {
        "Name": "Web Server Load Balancer",
        "ListenPort": 8080,
        "Backends": [...]
      },
      {
        "Name": "Database Load Balancer",
        "ListenPort": 3306,
        "Backends": [...]
      }
    ]
  }
}
```

## Usage

### Starting the Load Balancer

1. Configure your `appsettings.json` file with your backend servers
2. Run the application:
```bash
dotnet run --project MillenniumLoadBalancer.App
```

The load balancer will:
- Start listening on the configured ports
- Begin health checking all backend servers
- Forward incoming connections to healthy backends using the selected strategy
- Automatically remove unhealthy backends from rotation
- Attempt to recover unhealthy backends at the configured interval

### Stopping the Load Balancer

Press `Ctrl+C` to gracefully shut down the load balancer. All connections will be closed and resources cleaned up.

### Logging

Logs are written to:
- **Windows**: `%ProgramData%\MillenniumLoadBalancer\Logs\loadbalancer-YYYYMMDD.log`
- **Linux**: `/var/log/millenniumloadbalancer/loadbalancer-YYYYMMDD.log` (or fallback to temp directory)
- **Console**: Standard output with timestamps

Log files are automatically rotated daily.

## Testing

The project includes comprehensive unit and integration tests.

### Run Unit Tests

```bash
dotnet test Tests/MillenniumLoadBalancer.UnitTests/MillenniumLoadBalancer.UnitTests/MillenniumLoadBalancer.UnitTests.csproj
```

### Run Integration Tests

```bash
dotnet test Tests/MillenniumLoadBalancer.IntegrationTests/MillenniumLoadBalancer.IntegrationTests/MillenniumLoadBalancer.IntegrationTests.csproj
```

### Run All Tests

```bash
dotnet test
```

## CI/CD

The project includes GitHub Actions workflows for continuous integration. Tests run automatically on:
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches

## Project Structure

```
MillenniumLoadBalancer/
├── MillenniumLoadBalancer.App/          # Main application
│   ├── Core/                            # Core interfaces and strategies
│   ├── Infrastructure/                  # Implementations and factories
│   ├── Program.cs                       # Application entry point
│   └── appsettings.json                 # Configuration file
├── Tests/
│   ├── MillenniumLoadBalancer.UnitTests/      # Unit tests
│   └── MillenniumLoadBalancer.IntegrationTests/ # Integration tests
└── MillenniumLoadBalancer.TestApp/      # WPF test application
```

## Extending the Load Balancer

### Adding Custom Load Balancing Strategies

1. Implement `ILoadBalancingStrategy`:
```csharp
public class CustomStrategy : ILoadBalancingStrategy
{
    public IBackendService? SelectBackend(IEnumerable<IBackendService> backends)
    {
        // Your selection logic here
    }
}
```

2. Register your strategy in the `LoadBalancingStrategyFactory`

### Adding Custom Protocols

1. Implement `IConnectionForwarder` for your protocol
2. Register it in the `ConnectionForwarderFactory`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

MIT License

Copyright (c) 2025 Bradley Sewell

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Support

For issues, questions, or contributions, please open an issue on the GitHub repository.
