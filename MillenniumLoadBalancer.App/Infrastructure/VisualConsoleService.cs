using Microsoft.Extensions.Configuration;
using MillenniumLoadBalancer.App.Core.Configuration;
using MillenniumLoadBalancer.App.Core.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace MillenniumLoadBalancer.App.Infrastructure;

/// <summary>
/// Visual console service with millennium-style terminal game design.
/// </summary>
internal class VisualConsoleService : IVisualConsoleService
{
    private const int LineWidth = 79; // Fixed line width for all lines
    
    private readonly IConnectionTracker _connectionTracker;
    private readonly IConfiguration _configuration;
    private readonly bool _enableVisualMode;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly object _consoleLock = new();
    
    private enum Alignment
    {
        Left,
        Center,
        Right
    }

    public VisualConsoleService(IConnectionTracker connectionTracker, IConfiguration configuration)
    {
        _connectionTracker = connectionTracker;
        _configuration = configuration;
        
        // Check if visual mode is enabled
        var loadBalancerOptions = _configuration.GetSection("LoadBalancer").Get<LoadBalancerOptions>();
        _enableVisualMode = loadBalancerOptions?.EnableVisualMode ?? false;
    }

    public void Initialize()
    {
        if (_enableVisualMode)
        {
            Console.Clear();
            Console.CursorVisible = false;
        }
        else
        {
            // Show heading when visual mode is disabled (no datetime)
            ShowHeading(showDateTime: false);
        }
    }
    
    private void ShowHeading(StringBuilder? sb = null, bool showDateTime = false)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionString = version != null 
            ? $"v{version.Major}.{version.Minor}"
            : "v1.0";
        
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        if (sb != null)
        {
            // Append to StringBuilder (for RenderDashboard)
            sb.AppendLine(CreateHorizontalLine('╔', '═', '╗'));
            sb.AppendLine(CreateLine("║ ", "████  MILLENNIUM LOAD BALANCER  ████", " ║", Alignment.Center));
            sb.AppendLine(CreateLine("║ ", $"[SYSTEM VERSION {versionString}]", " ║", Alignment.Center));
            if (showDateTime)
            {
                sb.AppendLine(CreateLine("║ ", $"{timestamp}", " ║", Alignment.Center));
            }
            sb.AppendLine(CreateHorizontalLine('╚', '═', '╝'));
            if (showDateTime)
            {
                sb.AppendLine();
            }
        }
        else
        {
            // Write directly to Console (for Initialize)
            Console.WriteLine(CreateHorizontalLine('╔', '═', '╗'));
            Console.WriteLine(CreateLine("║ ", "████  MILLENNIUM LOAD BALANCER  ████", " ║", Alignment.Center));
            Console.WriteLine(CreateLine("║ ", $"[SYSTEM VERSION {versionString}]", " ║", Alignment.Center));
            if (showDateTime)
            {
                Console.WriteLine(CreateLine("║ ", $"{timestamp}", " ║", Alignment.Center));
            }
            Console.WriteLine(CreateHorizontalLine('╚', '═', '╝'));
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!_enableVisualMode)
        {
            return; // Don't run visual loop if visual mode is disabled
        }
        
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        try
        {
            await DisplayLoopAsync(_cancellationTokenSource.Token);
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }

    private async Task DisplayLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (_consoleLock)
                {
                    RenderDashboard();
                }
                
                await Task.Delay(1000, cancellationToken); // Update once per second
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }

    /// <summary>
    /// Creates a formatted line with specified alignment.
    /// </summary>
    private string CreateLine(string left, string content, string right, Alignment alignment = Alignment.Left)
    {
        var availableWidth = LineWidth - left.Length - right.Length;
        var contentLength = content.Length;
        
        // Truncate content if too long
        if (contentLength > availableWidth)
        {
            content = content.Substring(0, availableWidth);
            contentLength = availableWidth;
        }
        
        var padding = availableWidth - contentLength;
        string paddedContent;
        
        switch (alignment)
        {
            case Alignment.Center:
                var leftPad = padding / 2;
                var rightPad = padding - leftPad;
                paddedContent = $"{new string(' ', leftPad)}{content}{new string(' ', rightPad)}";
                break;
            case Alignment.Right:
                paddedContent = $"{new string(' ', padding)}{content}";
                break;
            case Alignment.Left:
            default:
                paddedContent = $"{content}{new string(' ', padding)}";
                break;
        }
        
        return $"{left}{paddedContent}{right}";
    }
    
    /// <summary>
    /// Creates a horizontal border line.
    /// </summary>
    private string CreateHorizontalLine(char left, char fill, char right)
    {
        return $"{left}{new string(fill, LineWidth - 2)}{right}";
    }

    private void RenderDashboard()
    {
        var stats = _connectionTracker.GetStatistics();
        
        // Clear console and reset cursor to top-left
        Console.Clear();
        Console.SetCursorPosition(0, 0);
        
        var sb = new StringBuilder();
        
        // Header with millennium style (with datetime for visual mode)
        ShowHeading(sb, showDateTime: true);
        
        // Global statistics with millennium style
        sb.AppendLine(CreateHorizontalLine('┌', '─', '┐'));
        sb.AppendLine(CreateLine("│ ", "═══ GLOBAL STATISTICS ═══", " │", Alignment.Center));
        sb.AppendLine(CreateHorizontalLine('├', '─', '┤'));
        sb.AppendLine(CreateLine("│ ", $"[ACCEPTED]  {stats.TotalConnectionsAccepted,10} connections", " │", Alignment.Left));
        sb.AppendLine(CreateLine("│ ", $"[FORWARDED] {stats.TotalConnectionsForwarded,10} connections", " │", Alignment.Left));
        sb.AppendLine(CreateLine("│ ", $"[FAILED]    {stats.TotalConnectionsFailed,10} connections", " │", Alignment.Left));
        sb.AppendLine(CreateLine("│ ", $"[ACTIVE]    {stats.TotalActiveConnections,10} connections", " │", Alignment.Left));
        sb.AppendLine(CreateHorizontalLine('└', '─', '┘'));
        sb.AppendLine();
        
        // Per load balancer statistics
        foreach (var lbKvp in stats.LoadBalancers)
        {
            var lbStats = lbKvp.Value;
            var listenerName = lbStats.Name.Length > 50 ? lbStats.Name.Substring(0, 50) : lbStats.Name;
            
            sb.AppendLine(CreateHorizontalLine('┌', '─', '┐'));
            sb.AppendLine(CreateLine("│ ", $"═══ LISTENER: {listenerName} ═══", " │", Alignment.Center));
            sb.AppendLine(CreateHorizontalLine('├', '─', '┤'));
            
            var statsLine = $"Accepted: {lbStats.ConnectionsAccepted,8} │ Forwarded: {lbStats.ConnectionsForwarded,8} │ Failed: {lbStats.ConnectionsFailed,8} │ Active: {lbStats.ActiveConnections,8}";
            sb.AppendLine(CreateLine("│ ", statsLine, " │", Alignment.Left));
            sb.AppendLine(CreateHorizontalLine('├', '─', '┤'));
            sb.AppendLine(CreateLine("│ ", "BACKEND SERVERS", " │", Alignment.Center));
            sb.AppendLine(CreateHorizontalLine('├', '─', '┤'));
            
            if (lbStats.Backends.Count == 0)
            {
                sb.AppendLine(CreateLine("│ ", "[WARNING] No backends configured", " │", Alignment.Left));
            }
            else
            {
                foreach (var backendKvp in lbStats.Backends.OrderBy(b => b.Value.Address).ThenBy(b => b.Value.Port))
                {
                    var backend = backendKvp.Value;
                    var statusIcon = backend.IsHealthy ? "█" : "░";
                    var statusText = backend.IsHealthy ? "ONLINE" : "OFFLINE";
                    var backendAddr = $"{backend.Address}:{backend.Port}";
                    if (backendAddr.Length > 18) backendAddr = backendAddr.Substring(0, 18);
                    
                    var backendLine = $"  [{statusIcon}] {backendAddr,-18} │ {statusText,-6} │ Fwd: {backend.ConnectionsForwarded,6} │ Fail: {backend.ConnectionsFailed,6}";
                    sb.AppendLine(CreateLine("│", backendLine, "│", Alignment.Left));
                }
            }
            
            sb.AppendLine(CreateHorizontalLine('└', '─', '┘'));
            sb.AppendLine();
        }
        
        // Footer
        sb.AppendLine(CreateHorizontalLine('┌', '─', '┐'));
        sb.AppendLine(CreateLine("│ ", "[CTRL+C] Shutdown System", " │", Alignment.Center));
        sb.AppendLine(CreateHorizontalLine('└', '─', '┘'));
        
        // Write the complete output
        Console.Write(sb.ToString());
    }
}

