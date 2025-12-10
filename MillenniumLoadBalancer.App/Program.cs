using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Configuration;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure;

namespace MillenniumLoadBalancer;

internal class Program
{
    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var serviceProvider = ServiceConfiguration.ConfigureServices();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var visualConsoleService = serviceProvider.GetRequiredService<IVisualConsoleService>();
        var loadBalancerManager = serviceProvider.GetRequiredService<ILoadBalancerManager>();

        try
        {
            if (serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider is null after configuration");
            }

            if (visualConsoleService == null)
            {
                throw new InvalidOperationException("Visual console service is null after retrieval from service provider");
            }

            if (loadBalancerManager == null)
            {
                throw new InvalidOperationException("Load balancer manager is null after retrieval from service provider");
            }

            visualConsoleService.Initialize(); //this shows the header even if visual mode is disabled

            loadBalancerManager.Initialize();
                        
            await loadBalancerManager.StartAllAsync(cts.Token);
        }
        catch (Exception ex)
        {
            LogError(serviceProvider, ex, "Failed to initialize load balancer");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }

        try
        {
            var loadBalancerOptions = configuration.GetSection("LoadBalancer").Get<LoadBalancerOptions>();
            var enableVisualMode = loadBalancerOptions?.EnableVisualMode ?? false;

            if (enableVisualMode)
            {
                await visualConsoleService.RunAsync(cts.Token);
            }
            else
            {
                // If visual mode is disabled, built in logger will write to console.
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // ctrl+c pressed
        }
        catch (Exception ex)
        {
            LogError(serviceProvider, ex, "Fatal error in load balancer");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        finally
        {
            try
            {
                visualConsoleService.Stop();
            }
            catch (Exception ex)
            {
                LogError(serviceProvider, ex, "Error stopping visual console");
            }
            
            try
            {
                await loadBalancerManager.StopAllAsync(cts.Token);
            }
            catch (Exception ex)
            {
                LogError(serviceProvider, ex, "Error during shutdown");
            }
        }
    }

    private static void LogError(IServiceProvider? serviceProvider, Exception ex, string message)
    {
        if (serviceProvider != null)
        {
            try
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, message);
                return;
            }
            catch
            {
                // Logger not available, fall back to console
            }
        }
        Console.WriteLine($"{message}: {ex.Message}");
    }
}
