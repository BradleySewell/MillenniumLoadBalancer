using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Configuration;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Core.Services;
using MillenniumLoadBalancer.App.Infrastructure.Factories;
using MillenniumLoadBalancer.App.Infrastructure.Logging;

namespace MillenniumLoadBalancer.App.Infrastructure;

/// <summary>
/// Configures and builds the service provider with all required services.
/// </summary>
public static class ServiceConfiguration
{
    private const int ShutdownTimeoutSeconds = 30;

    /// <summary>
    /// Configures and builds the service provider with all required services.
    /// </summary>
    public static IServiceProvider ConfigureServices(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();

        if (configuration == null)
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
        }

        services.AddSingleton<IConfiguration>(configuration);
        
        var loadBalancerOptions = configuration.GetSection("LoadBalancer").Get<LoadBalancerOptions>();
        var enableVisualMode = loadBalancerOptions?.EnableVisualMode ?? false;
        
        services.AddLogging(builder =>
        {
            // Only add console logging if visual mode is disabled
            if (!enableVisualMode)
            {
                builder.AddConsole(options =>
                {
                    options.FormatterName = "simple";
                });
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = false;
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
            }

            var logDirectory = GetLogDirectory();
            var logFileName = $"loadbalancer-{DateTime.Now.ToString("yyyyMMdd")}.log";
            var logFilePath = Path.Combine(logDirectory, logFileName);
            builder.AddProvider(new FileLoggerProvider(logFilePath, LogLevel.Information));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddSingleton<IConnectionTracker, ConnectionTracker>();
        services.AddSingleton<IVisualConsoleService, VisualConsoleService>();

        services.AddSingleton<IBackendHealthCheckService, BackendHealthCheckService>();
        services.AddSingleton<ILoadBalancingStrategyFactory, LoadBalancingStrategyFactory>();
        services.AddSingleton<IBackendServiceFactory, BackendServiceFactory>();
        services.AddSingleton<IConnectionForwarderFactory, ConnectionForwarderFactory>();
        services.AddSingleton<ILoadBalancerFactory, LoadBalancerFactory>();
        services.AddSingleton<ILoadBalancerManager, LoadBalancerManager>();

        return services.BuildServiceProvider();
    }

    private static string GetLogDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "MillenniumLoadBalancer", "Logs");
        }
        else
        {
            var systemLogDir = "/var/log/millenniumloadbalancer";
            
            try
            {
                if (!Directory.Exists(systemLogDir))
                {
                    Directory.CreateDirectory(systemLogDir);
                }
                
                // Test write 
                var testFile = Path.Combine(systemLogDir, ".write-test");
                try
                {
                    File.WriteAllText(testFile, "");
                    File.Delete(testFile);
                    return systemLogDir;
                }
                catch
                {
                }
            }
            catch
            {
            }
            
            // Fallback to temp dir
            var tempDir = Path.GetTempPath();
            if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
            {
                return Path.Combine(tempDir, "millenniumloadbalancer", "logs");
            }
            
            // Last resort is to use current directory. needed for running unit tests in github actions
            return Path.Combine(Directory.GetCurrentDirectory(), "logs");
        }
    }

    public static int ShutdownTimeout => ShutdownTimeoutSeconds;
}
