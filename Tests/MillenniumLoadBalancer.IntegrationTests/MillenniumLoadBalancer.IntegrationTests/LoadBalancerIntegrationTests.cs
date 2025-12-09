using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Configuration;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure;
using MillenniumLoadBalancer.IntegrationTests.Helpers;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace MillenniumLoadBalancer.IntegrationTests;

[TestClass]
public sealed class LoadBalancerIntegrationTests : IDisposable
{
    private IServiceProvider? _serviceProvider;
    private ILoadBalancerManager? _loadBalancerManager;
    private readonly List<MockBackendServer> _backendServers = new();
    private readonly List<MockTlsBackendServer> _tlsBackendServers = new();
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

        foreach (var server in _tlsBackendServers)
        {
            server.Dispose();
        }
        _tlsBackendServers.Clear();

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private IConfiguration CreateConfiguration(List<MockBackendServer> backends, string strategy = "RoundRobin", int recoveryCheckIntervalSeconds = 1, int recoveryDelaySeconds = 2, bool enableTls = false, bool validateCertificate = false)
    {
        var backendConfigs = backends.Select(b => new BackendConfiguration
        {
            Address = b.Address,
            Port = b.Port,
            EnableTls = enableTls,
            ValidateCertificate = validateCertificate
        }).ToList();

        return CreateConfigurationFromBackendConfigs(backendConfigs, strategy, recoveryCheckIntervalSeconds, recoveryDelaySeconds);
    }

    private IConfiguration CreateConfigurationFromTlsBackends(List<MockTlsBackendServer> tlsBackends, string strategy = "RoundRobin", int recoveryCheckIntervalSeconds = 1, int recoveryDelaySeconds = 2)
    {
        var backendConfigs = tlsBackends.Select(b => new BackendConfiguration
        {
            Address = b.Address,
            Port = b.Port,
            EnableTls = true,
            ValidateCertificate = false // Accept self-signed certs in tests
        }).ToList();

        return CreateConfigurationFromBackendConfigs(backendConfigs, strategy, recoveryCheckIntervalSeconds, recoveryDelaySeconds);
    }

    private IConfiguration CreateConfigurationFromBackendConfigs(List<BackendConfiguration> backendConfigs, string strategy, int recoveryCheckIntervalSeconds, int recoveryDelaySeconds)
    {
        var listenerConfig = new ListenerConfiguration
        {
            Name = "TestLoadBalancer",
            Protocol = "TCP",
            Strategy = strategy,
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
            configDict[$"LoadBalancer:Listeners:0:Backends:{i}:EnableTls"] = backendConfigs[i].EnableTls.ToString();
            configDict[$"LoadBalancer:Listeners:0:Backends:{i}:ValidateCertificate"] = backendConfigs[i].ValidateCertificate.ToString();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict.Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value.ToString())))
            .Build();

        return configuration;
    }

    private async Task SetupLoadBalancerAsync(int backendCount, string strategy = "RoundRobin", int recoveryCheckIntervalSeconds = 1, int recoveryDelaySeconds = 2)
    {
        for (int i = 0; i < backendCount; i++)
        {
            var server = new MockBackendServer();
            await server.StartAsync();
            _backendServers.Add(server);
        }

        var configuration = CreateConfiguration(_backendServers, strategy, recoveryCheckIntervalSeconds, recoveryDelaySeconds);

        _serviceProvider = ServiceConfiguration.ConfigureServices(configuration);

        var loggingBuilder = _serviceProvider.GetRequiredService<ILoggerFactory>();

        _loadBalancerManager = _serviceProvider.GetRequiredService<ILoadBalancerManager>();
        _loadBalancerManager.Initialize();
        await _loadBalancerManager.StartAllAsync();

        await Task.Delay(500);
    }

    private async Task SetupTlsLoadBalancerAsync(int backendCount, string strategy = "RoundRobin", int recoveryCheckIntervalSeconds = 1, int recoveryDelaySeconds = 2)
    {
        for (int i = 0; i < backendCount; i++)
        {
            var server = new MockTlsBackendServer();
            await server.StartAsync();
            _tlsBackendServers.Add(server);
        }

        var configuration = CreateConfigurationFromTlsBackends(_tlsBackendServers, strategy, recoveryCheckIntervalSeconds, recoveryDelaySeconds);

        _serviceProvider = ServiceConfiguration.ConfigureServices(configuration);

        var loggingBuilder = _serviceProvider.GetRequiredService<ILoggerFactory>();

        _loadBalancerManager = _serviceProvider.GetRequiredService<ILoadBalancerManager>();
        _loadBalancerManager.Initialize();
        await _loadBalancerManager.StartAllAsync();

        await Task.Delay(1000); // Wait for TLS health checks
    }


    [TestMethod]
    public async Task LoadBalancer_Strategy_RoundRobin_DistributesConnectionsEvenly()
    {
        await SetupLoadBalancerAsync(backendCount: 3);

        var messages = new List<string>();
        var connections = new List<TcpClient>();
        for (int i = 0; i < 6; i++)
        {
            var client = new TcpClient();
            await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
            await Task.Delay(50);
            var stream = client.GetStream();
            var testMessage = $"RoundRobin message {i}";
            messages.Add(testMessage);
            var testBytes = Encoding.UTF8.GetBytes(testMessage);
            await stream.WriteAsync(testBytes);
            
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.AreEqual(testMessage, response, $"Response should match sent message for connection {i}");
            
            connections.Add(client);
        }

        foreach (var client in connections)
        {
            client.Close();
        }

        await Task.Delay(200); 

        var connectionCounts = _backendServers.Select(s => s.ConnectionCount).ToList();
        Assert.IsTrue(connectionCounts.All(c => c > 0), "All backends should have received at least one connection");
        Assert.IsGreaterThanOrEqualTo(6, connectionCounts.Sum(), "Total connections should be at least 6");
        
        var maxConnections = connectionCounts.Max();
        var minConnections = connectionCounts.Min();
        var difference = maxConnections - minConnections;
        Assert.IsLessThanOrEqualTo(1, difference, 
            $"Connections should be distributed evenly. Max: {maxConnections}, Min: {minConnections}, Difference: {difference}");
        
        // Verify all messages were received by backends
        var allReceivedMessages = _backendServers.SelectMany(s => s.ReceivedMessages).ToList();
        foreach (var message in messages)
        {
            Assert.Contains(message, allReceivedMessages, $"Message '{message}' should have been received by a backend");
        }
    }

    [TestMethod]
    public async Task LoadBalancer_Strategy_RoundRobin_Ssl_DistributesConnectionsEvenly()
    {
        await SetupTlsLoadBalancerAsync(backendCount: 3, strategy: "RoundRobin");

        var messages = new List<string>();
        var connections = new List<(TcpClient client, SslStream sslStream)>();
        for (int i = 0; i < 6; i++)
        {
            var client = new TcpClient();
            await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
            var stream = client.GetStream();
            var sslStream = new SslStream(stream, false, (sender, certificate, chain, sslPolicyErrors) => true, null);
            await sslStream.AuthenticateAsClientAsync("127.0.0.1");
            var testMessage = $"RoundRobin TLS message {i}";
            messages.Add(testMessage);
            var testBytes = Encoding.UTF8.GetBytes(testMessage);
            await sslStream.WriteAsync(testBytes);
            
            var buffer = new byte[4096];
            var bytesRead = await sslStream.ReadAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.AreEqual(testMessage, response, $"Response should match sent message for TLS connection {i}");
            
            connections.Add((client, sslStream));
            await Task.Delay(50);
        }

        foreach (var (client, sslStream) in connections)
        {
            try
            {
                sslStream.Dispose();
                client.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }

        await Task.Delay(200);

        var connectionCounts = _tlsBackendServers.Select(s => s.ConnectionCount).ToList();
        Assert.IsTrue(connectionCounts.All(c => c > 0), "All TLS backends should have received at least one connection");
        Assert.IsGreaterThanOrEqualTo(6, connectionCounts.Sum(), "Total connections should be at least 6");
        
        var maxConnections = connectionCounts.Max();
        var minConnections = connectionCounts.Min();
        var difference = maxConnections - minConnections;
        Assert.IsLessThanOrEqualTo(1, difference, 
            $"Connections should be distributed evenly. Max: {maxConnections}, Min: {minConnections}, Difference: {difference}");
        
        // Verify all messages were received by backends
        var allReceivedMessages = _tlsBackendServers.SelectMany(s => s.ReceivedMessages).ToList();
        foreach (var message in messages)
        {
            Assert.Contains(message, allReceivedMessages, $"Message '{message}' should have been received by a TLS backend");
        }
    }

    [TestMethod]
    public async Task LoadBalancer_ForwardsData_Bidirectionally_WithRoundRobinStrategy()
    {
        await SetupLoadBalancerAsync(backendCount: 1, strategy: "RoundRobin");

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
    public async Task LoadBalancer_MarksBackendUnhealthy_WhenConnectionFails_WithRoundRobinStrategy()
    {
        await SetupLoadBalancerAsync(backendCount: 2, strategy: "RoundRobin", recoveryCheckIntervalSeconds: 1, recoveryDelaySeconds: 1);

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
    public async Task LoadBalancer_RecoversBackend_WhenItBecomesHealthy_WithRoundRobinStrategy()
    {
        await SetupLoadBalancerAsync(backendCount: 2, strategy: "RoundRobin", recoveryCheckIntervalSeconds: 1, recoveryDelaySeconds: 1);

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
    public async Task LoadBalancer_HandlesMultipleConcurrentConnections_WithRoundRobinStrategy()
    {
        await SetupLoadBalancerAsync(backendCount: 2, strategy: "RoundRobin");

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
        Assert.IsGreaterThanOrEqualTo(10, totalConnections, $"Expected at least 10 connections, got {totalConnections}");
        
        var allReceivedMessages = _backendServers.SelectMany(s => s.ReceivedMessages).ToList();
        for (int i = 0; i < 10; i++)
        {
            var expectedMessage = $"Concurrent message {i}";
            Assert.Contains(expectedMessage, allReceivedMessages, $"Expected message '{expectedMessage}' to be received by a backend");
        }
    }

    [TestMethod]
    public async Task LoadBalancer_Strategy_Random_DistributesConnectionsAcrossAllBackends()
    {
        await SetupLoadBalancerAsync(backendCount: 3, strategy: "Random");

        var messages = new List<string>();
        var connections = new List<TcpClient>();
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
                var stream = client.GetStream();
                var testMessage = $"Random message {i}";
                messages.Add(testMessage);
                var testBytes = Encoding.UTF8.GetBytes(testMessage);
                await stream.WriteAsync(testBytes);
                
                var buffer = new byte[4096];
                var bytesRead = await stream.ReadAsync(buffer);
                var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Assert.AreEqual(testMessage, response, $"Response should match sent message for connection {i}");
                
                connections.Add(client);
                await Task.Delay(30);
            }
            catch
            {
                // Ignore connection errors
            }
        }

        await Task.Delay(300);

        foreach (var client in connections)
        {
            try
            {
                client.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }

        await Task.Delay(300);

        var connectionCounts = _backendServers.Select(s => s.ConnectionCount).ToList();
        Assert.IsTrue(connectionCounts.All(c => c > 0), $"All backends should have received at least one connection. Connection counts: [{string.Join(", ", connectionCounts)}]");
        Assert.IsGreaterThanOrEqualTo(20, connectionCounts.Sum(), $"Total connections should be at least 20, got {connectionCounts.Sum()}");
        
        // Verify all messages were received by backends
        var allReceivedMessages = _backendServers.SelectMany(s => s.ReceivedMessages).ToList();
        foreach (var message in messages)
        {
            Assert.Contains(message, allReceivedMessages, $"Message '{message}' should have been received by a backend");
        }
    }

    [TestMethod]
    public async Task LoadBalancer_Strategy_Random_Ssl_DistributesConnectionsAcrossAllBackends()
    {
        await SetupTlsLoadBalancerAsync(backendCount: 3, strategy: "Random");

        var messages = new List<string>();
        var connections = new List<(TcpClient client, SslStream sslStream)>();
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
                var stream = client.GetStream();
                var sslStream = new SslStream(stream, false, (sender, certificate, chain, sslPolicyErrors) => true, null);
                await sslStream.AuthenticateAsClientAsync("127.0.0.1");
                var testMessage = $"Random TLS message {i}";
                messages.Add(testMessage);
                var testBytes = Encoding.UTF8.GetBytes(testMessage);
                await sslStream.WriteAsync(testBytes);
                
                var buffer = new byte[4096];
                var bytesRead = await sslStream.ReadAsync(buffer);
                var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Assert.AreEqual(testMessage, response, $"Response should match sent message for TLS connection {i}");
                
                connections.Add((client, sslStream));
                await Task.Delay(30);
            }
            catch
            {
                // Ignore connection errors
            }
        }

        await Task.Delay(300);

        foreach (var (client, sslStream) in connections)
        {
            try
            {
                sslStream.Dispose();
                client.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }

        await Task.Delay(300);

        var connectionCounts = _tlsBackendServers.Select(s => s.ConnectionCount).ToList();
        Assert.IsTrue(connectionCounts.All(c => c > 0), $"All TLS backends should have received at least one connection. Connection counts: [{string.Join(", ", connectionCounts)}]");
        Assert.IsGreaterThanOrEqualTo(20, connectionCounts.Sum(), $"Total connections should be at least 20, got {connectionCounts.Sum()}");
        
        // Verify all messages were received by backends
        var allReceivedMessages = _tlsBackendServers.SelectMany(s => s.ReceivedMessages).ToList();
        foreach (var message in messages)
        {
            Assert.Contains(message, allReceivedMessages, $"Message '{message}' should have been received by a TLS backend");
        }
    }

    [TestMethod]
    public async Task LoadBalancer_Strategy_Fallback_UsesFirstBackendWhenAllHealthy()
    {
        await SetupLoadBalancerAsync(backendCount: 3, strategy: "Fallback");

        var messages = new List<string>();
        var clients = new List<TcpClient>();
        
        for (int i = 0; i < 10; i++)
        {
            var client = new TcpClient();
            clients.Add(client);
            await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
            await Task.Delay(50); // Ensure connection is established
            var stream = client.GetStream();
            var testMessage = $"test {i}";
            messages.Add(testMessage);
            var testBytes = Encoding.UTF8.GetBytes(testMessage);
            await stream.WriteAsync(testBytes);
            
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.AreEqual(testMessage, response);
            
            // Check messages immediately after each response to ensure they're recorded
            await Task.Delay(100); // Small delay to ensure backend has processed
        }

        // Keep connections open a bit longer to ensure all data is processed
        await Task.Delay(500);
        
        // Check messages before closing connections
        var firstBackendMessages = _backendServers[0].ReceivedMessages;
        Assert.IsGreaterThan(0, firstBackendMessages.Count, $"First backend should have received messages. Got {firstBackendMessages.Count} messages");
        
        // Close connections
        foreach (var client in clients)
        {
            try
            {
                client.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }
        
        await Task.Delay(500);
        
        var totalMessages = _backendServers.Sum(s => s.ReceivedMessages.Count);
        Assert.IsGreaterThanOrEqualTo(10, totalMessages, $"Expected at least 10 messages, got {totalMessages}");
        
        Assert.HasCount(totalMessages, firstBackendMessages, 
            $"Fallback strategy should use only the first healthy backend when all are healthy. First backend: {firstBackendMessages.Count}, Total: {totalMessages}, Backend counts: [{string.Join(", ", _backendServers.Select(s => s.ReceivedMessages.Count))}]");
        
        foreach (var message in messages)
        {
            Assert.Contains(message, firstBackendMessages, $"Message '{message}' should be in first backend");
        }
    }

    [TestMethod]
    public async Task LoadBalancer_Strategy_Fallback_Ssl_UsesFirstBackendWhenAllHealthy()
    {
        await SetupTlsLoadBalancerAsync(backendCount: 3, strategy: "Fallback");

        var messages = new List<string>();
        var connections = new List<(TcpClient client, SslStream sslStream)>();
        
        for (int i = 0; i < 10; i++)
        {
            var client = new TcpClient();
            await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
            await Task.Delay(50);
            var stream = client.GetStream();
            var sslStream = new SslStream(stream, false, (sender, certificate, chain, sslPolicyErrors) => true, null);
            await sslStream.AuthenticateAsClientAsync("127.0.0.1");
            var testMessage = $"test {i}";
            messages.Add(testMessage);
            var testBytes = Encoding.UTF8.GetBytes(testMessage);
            await sslStream.WriteAsync(testBytes);
            
            var buffer = new byte[4096];
            var bytesRead = await sslStream.ReadAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.AreEqual(testMessage, response);
            
            connections.Add((client, sslStream));
            await Task.Delay(100);
        }

        await Task.Delay(500);
        
        var firstBackendMessages = _tlsBackendServers[0].ReceivedMessages;
        Assert.IsGreaterThan(0, firstBackendMessages.Count, $"First TLS backend should have received messages. Got {firstBackendMessages.Count} messages");
        
        foreach (var (client, sslStream) in connections)
        {
            try
            {
                sslStream.Dispose();
                client.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }
        
        await Task.Delay(500);
        
        var totalMessages = _tlsBackendServers.Sum(s => s.ReceivedMessages.Count);
        Assert.IsGreaterThanOrEqualTo(10, totalMessages, $"Expected at least 10 messages, got {totalMessages}");
        
        Assert.HasCount(totalMessages, firstBackendMessages, 
            $"Fallback strategy should use only the first healthy TLS backend when all are healthy. First backend: {firstBackendMessages.Count}, Total: {totalMessages}, Backend counts: [{string.Join(", ", _tlsBackendServers.Select(s => s.ReceivedMessages.Count))}]");
        
        foreach (var message in messages)
        {
            Assert.Contains(message, firstBackendMessages, $"Message '{message}' should be in first TLS backend");
        }
    }

    [TestMethod]
    public async Task LoadBalancer_Strategy_Fallback_MovesToNextBackend_WhenFirstFails()
    {
        await SetupLoadBalancerAsync(backendCount: 3, strategy: "Fallback", recoveryCheckIntervalSeconds: 1, recoveryDelaySeconds: 1);

        var firstBackend = _backendServers[0];
        var secondBackend = _backendServers[1];
        var thirdBackend = _backendServers[2];

        var initialFirstMessages = firstBackend.ReceivedMessages.Count;
        var initialClients = new List<TcpClient>();

        // Send initial messages to first backend
        for (int i = 0; i < 5; i++)
        {
            var client = new TcpClient();
            initialClients.Add(client);
            await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
            await Task.Delay(50);
            var stream = client.GetStream();
            var testMessage = $"test {i}";
            var testBytes = Encoding.UTF8.GetBytes(testMessage);
            await stream.WriteAsync(testBytes);
            
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.AreEqual(testMessage, response);
            await Task.Delay(50);
        }

        await Task.Delay(500);
        
        // Check messages before closing connections
        Assert.IsGreaterThan(initialFirstMessages, firstBackend.ReceivedMessages.Count, 
            $"First backend should have received messages initially. Initial: {initialFirstMessages}, Current: {firstBackend.ReceivedMessages.Count}");
        
        // Close initial connections
        foreach (var client in initialClients)
        {
            try
            {
                client.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }
        
        await Task.Delay(500);

        // Stop first backend
        await firstBackend.StopAsync();
        
        // Wait for health check to mark it as unhealthy and for recovery delay
        await Task.Delay(2500);

        var secondBackendInitialMessages = secondBackend.ReceivedMessages.Count;
        var fallbackClients = new List<TcpClient>();

        // Send messages that should now go to second backend
        for (int i = 0; i < 5; i++)
        {
            try
            {
                var client = new TcpClient();
                fallbackClients.Add(client);
                await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
                await Task.Delay(50);
                var stream = client.GetStream();
                var testMessage = $"fallback {i}";
                var testBytes = Encoding.UTF8.GetBytes(testMessage);
                await stream.WriteAsync(testBytes);
                
                var buffer = new byte[4096];
                var bytesRead = await stream.ReadAsync(buffer);
                var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Assert.AreEqual(testMessage, response);
                await Task.Delay(50);
            }
            catch
            {
            }
        }

        await Task.Delay(500);
        
        // Check messages before closing connections
        Assert.IsGreaterThan(secondBackendInitialMessages, secondBackend.ReceivedMessages.Count, 
            $"Second backend should have received messages after first backend fails. Initial: {secondBackendInitialMessages}, Current: {secondBackend.ReceivedMessages.Count}, Backend counts: [{string.Join(", ", _backendServers.Select(s => s.ReceivedMessages.Count))}]");
        
        // Close fallback connections
        foreach (var client in fallbackClients)
        {
            try
            {
                client.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }
        
        await Task.Delay(500);
    }

    [TestMethod]
    public async Task LoadBalancer_Strategy_Fallback_Ssl_MovesToNextBackend_WhenFirstFails()
    {
        await SetupTlsLoadBalancerAsync(backendCount: 3, strategy: "Fallback", recoveryCheckIntervalSeconds: 1, recoveryDelaySeconds: 1);

        var firstBackend = _tlsBackendServers[0];
        var secondBackend = _tlsBackendServers[1];
        var thirdBackend = _tlsBackendServers[2];

        var initialFirstMessages = firstBackend.ReceivedMessages.Count;
        var initialClients = new List<(TcpClient client, SslStream sslStream)>();

        // Send initial messages to first backend
        for (int i = 0; i < 5; i++)
        {
            var client = new TcpClient();
            await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
            await Task.Delay(50);
            var stream = client.GetStream();
            var sslStream = new SslStream(stream, false, (sender, certificate, chain, sslPolicyErrors) => true, null);
            await sslStream.AuthenticateAsClientAsync("127.0.0.1");
            var testMessage = $"test {i}";
            var testBytes = Encoding.UTF8.GetBytes(testMessage);
            await sslStream.WriteAsync(testBytes);
            
            var buffer = new byte[4096];
            var bytesRead = await sslStream.ReadAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Assert.AreEqual(testMessage, response);
            initialClients.Add((client, sslStream));
            await Task.Delay(50);
        }

        await Task.Delay(500);
        
        // Check messages before closing connections
        Assert.IsGreaterThan(initialFirstMessages, firstBackend.ReceivedMessages.Count, 
            $"First TLS backend should have received messages initially. Initial: {initialFirstMessages}, Current: {firstBackend.ReceivedMessages.Count}");
        
        // Close initial connections
        foreach (var (client, sslStream) in initialClients)
        {
            try
            {
                sslStream.Dispose();
                client.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }
        
        await Task.Delay(500);

        // Stop first backend
        await firstBackend.StopAsync();
        
        // Wait for health check to mark it as unhealthy and for recovery delay
        await Task.Delay(2500);

        var secondBackendInitialMessages = secondBackend.ReceivedMessages.Count;
        var fallbackClients = new List<(TcpClient client, SslStream sslStream)>();

        // Send messages that should now go to second backend
        for (int i = 0; i < 5; i++)
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(LoadBalancerAddress, _loadBalancerPort);
                await Task.Delay(50);
                var stream = client.GetStream();
                var sslStream = new SslStream(stream, false, (sender, certificate, chain, sslPolicyErrors) => true, null);
                await sslStream.AuthenticateAsClientAsync("127.0.0.1");
                var testMessage = $"fallback {i}";
                var testBytes = Encoding.UTF8.GetBytes(testMessage);
                await sslStream.WriteAsync(testBytes);
                
                var buffer = new byte[4096];
                var bytesRead = await sslStream.ReadAsync(buffer);
                var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Assert.AreEqual(testMessage, response);
                fallbackClients.Add((client, sslStream));
                await Task.Delay(50);
            }
            catch
            {
            }
        }

        await Task.Delay(500);
        
        // Check messages before closing connections
        Assert.IsGreaterThan(secondBackendInitialMessages, secondBackend.ReceivedMessages.Count, 
            $"Second TLS backend should have received messages after first backend fails. Initial: {secondBackendInitialMessages}, Current: {secondBackend.ReceivedMessages.Count}, Backend counts: [{string.Join(", ", _tlsBackendServers.Select(s => s.ReceivedMessages.Count))}]");
        
        // Close fallback connections
        foreach (var (client, sslStream) in fallbackClients)
        {
            try
            {
                sslStream.Dispose();
                client.Close();
            }
            catch
            {
                // Ignore close errors
            }
        }
        
        await Task.Delay(500);
    }

    public void Dispose()
    {
        TestCleanup().Wait(TimeSpan.FromSeconds(5));
    }
}
