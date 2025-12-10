using MillenniumLoadBalancer.App.Core.Interfaces;

namespace MillenniumLoadBalancer.App.Core.Interfaces;

/// <summary>
/// Service for displaying a visual console dashboard.
/// </summary>
public interface IVisualConsoleService
{
    /// <summary>
    /// Initializes the visual console display.
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Runs the visual console display loop on the current thread.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops the visual console display.
    /// </summary>
    void Stop();
}

