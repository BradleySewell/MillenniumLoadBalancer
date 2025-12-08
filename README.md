# Millennium Load Balancer

A high-performance, production-ready TCP load balancer built with .NET 10.0. Millennium Load Balancer provides intelligent traffic distribution across multiple backend servers with health checking, automatic failover, and configurable load balancing strategies.

## Features

- **TCP Load Balancing**: Distribute TCP connections across multiple backend servers
- **Health Checking**: Automatic health monitoring with configurable intervals and recovery delays
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
            "Port": 5001
          },
          {
            "Address": "127.0.0.1",
            "Port": 5002
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
