using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace optimizerDuck.UI.Logger;

public class ServiceTracker : IDisposable
{
    private static readonly AsyncLocal<ServiceTracker?> _current = new();

    private readonly Dictionary<string, (int Success, int Fail)> _serviceStats;
    private readonly Stopwatch _stopwatch;

    private ServiceTracker(ILogger logger)
    {
        Log = logger;
        _stopwatch = Stopwatch.StartNew();
        _serviceStats = new Dictionary<string, (int Success, int Fail)>();
    }

    public static ServiceTracker? Current => _current.Value;

    public ILogger Log { get; }

    public void Dispose()
    {
        _stopwatch.Stop();
        var time = FormatTime(_stopwatch.Elapsed);

        var stats = string.Join(" [dim]|[/] ", _serviceStats.Select(kvp =>
            $"{kvp.Key}: [{Theme.Success}]{kvp.Value.Success}[/][dim]/[/][{Theme.Error}]{kvp.Value.Fail}[/]"));

        Log.LogInformation($"[{Theme.Muted}]Time: {time}[/] [dim]|[/] {stats}");

        _current.Value = null;
    }


    public static ServiceTracker Begin(ILogger? logger = null)
    {
        var log = logger ?? Logger.CreateLogger<ServiceTracker>();
        var tracker = new ServiceTracker(log);
        _current.Value = tracker;
        return tracker;
    }

    internal void Track(string serviceName, bool success)
    {
        if (!_serviceStats.ContainsKey(serviceName)) _serviceStats[serviceName] = (0, 0);

        var current = _serviceStats[serviceName];
        _serviceStats[serviceName] = success
            ? (current.Success + 1, current.Fail)
            : (current.Success, current.Fail + 1);
    }

    public static string FormatTime(TimeSpan time)
    {
        return time.TotalSeconds >= 1
            ? $"{time.TotalSeconds:F1}s"
            : time.TotalMilliseconds >= 1
                ? $"{time.TotalMilliseconds:F0}ms"
                : time.Ticks > 0
                    ? $"{time.Ticks} ticks"
                    : "0";
    }
}