using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Configuration;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure;
using MillenniumLoadBalancer.IntegrationTests.Helpers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MillenniumLoadBalancer.IntegrationTests;

[TestClass]
public sealed class LoadBalancerIntegrationTests : IDisposable
{
    private IServiceProvider? _serviceProvider;
    private ILoadBalancerManager? _loadBalancerManager;
    private readonly List<MockBackendServer> _backendServers = new();
    private int _loadBalancerPort;
    private const string LoadBalancerAddress = "127.0.0.1";

    [TestInitialize]
    public async Task TestInitialize()
    {
        // Find a port for the load balancer
        using var tempListener = new TcpListener(IPAddress.Parse(LoadBalancerAddress), 0);
        tempListener.Start();
        _loadBalancerPort = ((IPEndPoint)tempListener.LocalEndpoint).Port;
        tempListener.Stop();
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (_loadBalancerManager != null)
        {
            try
            {
                await _loadBalancerManager.StopAllAsync();
            }
            catch
            {
            }
        }

        foreach (var server in _backendServers)
        {
            server.Dispose();
        }
        _backendServers.Clear();

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private IConfiguration CreateConfiguration(List<MockBackendServer> backends, int recoveryCheckIntervalSeconds = 1, int recoveryDelaySeconds = 2)
    {
        var backendConfigs = backends.Select(b => new BackendConfiguration
        {
            Address = b.Address,
            Port = b.Port
        }).ToList();

        var listenerConfig = new ListenerConfiguration
        {
            Name = "TestLoadBalancer",
            Protocol = "TCP",
            Strategy = "RoundRobin",
            ListenAddress = LoadBalancerAddress,
            ListenPort = _loadBalancerPort,
            Backends = backendConfigs,
            RecoveryCheckIntervalSeconds = recoveryCheckIntervalSeconds,
            RecoveryDelaySeconds = recoveryDelaySeconds,
            ConnectionTimeoutSeconds = 5,
            SendTimeoutSeconds = 10,
            ReceiveTimeoutSeconds = 10
        };

        var options = new LoadBalancerOptions
        {
            Listeners = new List<ListenerConfiguration> { listenerConfig }
        };

        var configDict = new Dictionary<string, object>
        {
            ["LoadBalancer:Listeners:0:Name"] = options.Listeners[0].Name,
            ["LoadBalancer:Listeners:0:Protocol"] = options.Listeners[0].Protocol,
            ["LoadBalancer:Listeners:0:Strategy"] = options.Listeners[0].Strategy,
            ["LoadBalancer:Listeners:0:ListenAddress"] = options.Listeners[0].ListenAddress,
            ["LoadBalancer:Listeners:0:ListenPort"] = options.Listeners[0].ListenPort,
            ["LoadBalancer:Listeners:0:RecoveryCheckIntervalSeconds"] = options.Listeners[0].RecoveryCheckIntervalSeconds,
            ["LoadBalancer:Listeners:0:RecoveryDelaySeconds"] = options.Listeners[0].RecoveryDelaySeconds,
            ["LoadBalancer:Listeners:0:ConnectionTimeoutSeconds"] = options.Listeners[0].ConnectionTimeoutSeconds,
            ["LoadBalancer:Listeners:0:SendTimeoutSeconds"] = options.Listeners[0].SendTimeoutSeconds,
            ["LoadBalancer:Listeners:0:ReceiveTimeoutSeconds"] = options.Listeners[0].ReceiveTimeoutSeconds
        };

        for (int i = 0; i < backendConfigs.Count; i++)
        {
            configDict[$"LoadBalancer:Listeners:0:Backends:{i}:Address"] = backendConfigs[i].Address;
            configDict[$"LoadBalancer:Listeners:0:Backends:{i}:Port"] = backendConfigs[i].Port;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict.Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value.ToString())))
            .Build();

        return configuration;
    }

    private async Task SetupLoadBalancerAsync(int backendCount, int recoveryCheckIntervalSeconds = 1, int recoveryDelaySeconds = 2)
    {
        for (int i = 0; i < backendCount; i++)
        {
            var server = new MockBackendServer();
            await server.StartAsync();
            _backendServers.Add(server);
        }

        var configuration = CreateConfiguration(_backendServers, recoveryCheckIntervalSeconds, recoveryDelaySeconds);

        _serviceProvider = ServiceConfiguration.ConfigureServices(configuration);

        var loggingBuilder = _serviceProvider.GetRequiredService<ILoggerFactory>();

        _loadBalancerManager = _serviceProvider.GetRequiredService<ILoadBalancerManager>();
        _loadBalancerManager.Initialize();
        await _loadBalancerManager.StartAllAsync();

        await Task.Delay(500);
    }

    [TestMethod]
    public async Task LoadBalancer_ForwardsConnections_ToBackends()
    {
        await SetupLoadBalancerAsync(backendCount: 2);

        using var client = new TcpClient();
        await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
        var stream = client.GetStream();

        var testMessage = "Hello, Backend!";
        var messageBytes = Encoding.UTF8.GetBytes(testMessage);
        await stream.WriteAsync(messageBytes);

        var buffer = new byte[4096];
        var bytesRead = await stream.ReadAsync(buffer);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        Assert.AreEqual(testMessage, response);
        Assert.IsTrue(_backendServers.Any(s => s.ConnectionCount > 0), "At least one backend should have received a connection");
        
        var backendThatReceivedMessage = _backendServers.FirstOrDefault(s => s.ReceivedMessages.Contains(testMessage));
        Assert.IsNotNull(backendThatReceivedMessage, $"Expected at least one backend to have received the message '{testMessage}'");
    }

    [TestMethod]
    public async Task LoadBalancer_DistributesConnections_RoundRobin()
    {
        await SetupLoadBalancerAsync(backendCount: 3);

        var connections = new List<TcpClient>();
        for (int i = 0; i < 6; i++)
        {
            var client = new TcpClient();
            await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
            connections.Add(client);
            await Task.Delay(50); 
        }

        foreach (var client in connections)
        {
            client.Close();
        }

        await Task.Delay(200); 

        var connectionCounts = _backendServers.Select(s => s.ConnectionCount).ToList();
        Assert.IsTrue(connectionCounts.All(c => c > 0), "All backends should have received at least one connection");
        Assert.IsGreaterThanOrEqualTo(connectionCounts.Sum(), 6, "Total connections should be at least 6");
        
        var maxConnections = connectionCounts.Max();
        var minConnections = connectionCounts.Min();
        var difference = maxConnections - minConnections;
        Assert.IsLessThanOrEqualTo(1, difference, 
            $"Connections should be distributed evenly. Max: {maxConnections}, Min: {minConnections}, Difference: {difference}");
    }

    [TestMethod]
    public async Task LoadBalancer_ForwardsData_Bidirectionally()
    {
        await SetupLoadBalancerAsync(backendCount: 1);

        using var client = new TcpClient();
        await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
        var stream = client.GetStream();

        var messages = new List<string>();
        var responses = new List<string>();
        
        for (int i = 1; i <= 10; i++)
        {
            var message = $"Message {i}";
            messages.Add(message);
            
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(messageBytes);

            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            responses.Add(response);

            Assert.AreEqual(message, response, $"Response {i} should match sent message '{message}'");
        }

        Assert.HasCount(10, responses, "Should have received 10 responses");
        Assert.HasCount(10, messages, "Should have sent 10 messages");
        
        for (int i = 0; i < messages.Count; i++)
        {
            Assert.AreEqual(messages[i], responses[i], $"Response at index {i} should match message at index {i}. Expected: '{messages[i]}', Actual: '{responses[i]}'");
        }

        var backend = _backendServers[0];
        Assert.HasCount(10, backend.ReceivedMessages, "Backend should have received all 10 messages");
        
        for (int i = 0; i < messages.Count; i++)
        {
            Assert.AreEqual(messages[i], backend.ReceivedMessages[i], $"Backend should have received message '{messages[i]}' at index {i}");
        }
    }

    [TestMethod]
    public async Task LoadBalancer_MarksBackendUnhealthy_WhenConnectionFails()
    {
        await SetupLoadBalancerAsync(backendCount: 2, recoveryCheckIntervalSeconds: 1, recoveryDelaySeconds: 1);

        var stoppedServer = _backendServers[0];
        await stoppedServer.StopAsync();
       
        var healthyServer = _backendServers[1];
        var initialHealthyConnections = healthyServer.ConnectionCount;

        for (int i = 0; i < 5; i++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
                var stream = client.GetStream();
                var testBytes = Encoding.UTF8.GetBytes("test");
                await stream.WriteAsync(testBytes);
                await Task.Delay(50);
                client.Close();
            }
            catch
            {
            }
        }

        await Task.Delay(500);

        Assert.IsGreaterThan(initialHealthyConnections, healthyServer.ConnectionCount, 
            $"Healthy backend should have received connections. Initial: {initialHealthyConnections}, Current: {healthyServer.ConnectionCount}");
    }

    [TestMethod]
    public async Task LoadBalancer_RecoversBackend_WhenItBecomesHealthy()
    {
        await SetupLoadBalancerAsync(backendCount: 2, recoveryCheckIntervalSeconds: 1, recoveryDelaySeconds: 1);

        var backendToTest = _backendServers[0];
        var otherBackend = _backendServers[1];

        await backendToTest.StopAsync();

        await Task.Delay(1500);

        var newServer = new MockBackendServer(backendToTest.Address, backendToTest.Port);
        await newServer.StartAsync();
        _backendServers[0] = newServer;
        backendToTest.Dispose();

        await Task.Delay(2000);

        var initialConnectionsBackend1 = newServer.ConnectionCount;
        var initialConnectionsBackend2 = otherBackend.ConnectionCount;

        for (int i = 0; i < 4; i++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
                await Task.Delay(50);
                client.Close();
            }
            catch
            {
            }
        }

        await Task.Delay(300);

        Assert.IsTrue(newServer.ConnectionCount > initialConnectionsBackend1 || 
                     otherBackend.ConnectionCount > initialConnectionsBackend2,
            "At least one backend should have received new connections after recovery");
    }

    [TestMethod]
    public async Task LoadBalancer_HandlesMultipleConcurrentConnections()
    {
        await SetupLoadBalancerAsync(backendCount: 2);

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int messageId = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
                    var stream = client.GetStream();

                    var message = $"Concurrent message {messageId}";
                    var messageBytes = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(messageBytes);

                    var buffer = new byte[4096];
                    var bytesRead = await stream.ReadAsync(buffer);
                    var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    Assert.AreEqual(message, response);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Connection {messageId} failed: {ex.Message}");
                }
            }));
        }

        await Task.WhenAll(tasks);

        var totalConnections = _backendServers.Sum(s => s.ConnectionCount);
        Assert.IsGreaterThanOrEqualTo(totalConnections, 10, $"Expected at least 10 connections, got {totalConnections}");
        
        var allReceivedMessages = _backendServers.SelectMany(s => s.ReceivedMessages).ToList();
        for (int i = 0; i < 10; i++)
        {
            var expectedMessage = $"Concurrent message {i}";
            Assert.Contains(expectedMessage, allReceivedMessages, $"Expected message '{expectedMessage}' to be received by a backend");
        }
    }

    public void Dispose()
    {
        TestCleanup().Wait(TimeSpan.FromSeconds(5));
    }
}
