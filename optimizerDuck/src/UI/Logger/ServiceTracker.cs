using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace optimizerDuck.UI.Logger;

public class ServiceTracker : IDisposable
{
    private static readonly AsyncLocal<ServiceTracker?> _current = new();

    private readonly ConcurrentDictionary<string, (int Success, int Fail)> _serviceStats;
    private readonly Stopwatch _stopwatch;

    private ServiceTracker(ILogger logger)
    {
        Log = logger;
        _stopwatch = Stopwatch.StartNew();
        _serviceStats = new ConcurrentDictionary<string, (int Success, int Fail)>();
    }

    public static ServiceTracker? Current => _current.Value;

    public ILogger Log { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stopwatch.Stop();
            var time = FormatTime(_stopwatch.Elapsed);

            var stats = string.Join(" [dim]|[/] ", _serviceStats.Select(kvp =>
                $"{kvp.Key}: [{Theme.Success}]{kvp.Value.Success}[/][dim]/[/][{Theme.Error}]{kvp.Value.Fail}[/]"));

            Log.LogInformation($"[{Theme.Muted}]Time: {time}[/] [dim]|[/] {stats}");
            _current.Value = null;
        }
    }


    public static ServiceTracker Begin(ILogger? logger = null)
    {
        var log = logger ?? Logger.CreateLogger<ServiceTracker>();
        var tracker = new ServiceTracker(log);
        _current.Value = tracker;
        return tracker;
    }

    public void Track(string serviceName, bool success)
    {
        if (!_serviceStats.TryGetValue(serviceName, out var current))
            current = (0, 0);

        _serviceStats.AddOrUpdate(
            serviceName,
            success ? (1, 0) : (0, 1),
            (key, current) => success
                ? (current.Success + 1, current.Fail)
                : (current.Success, current.Fail + 1));
    }

    public static string FormatTime(TimeSpan time)
    {
        if (time.TotalSeconds >= 1) return $"{time.TotalSeconds:F1}s";
        if (time.TotalMilliseconds >= 1) return $"{time.TotalMilliseconds:F0}ms";
        return time.Ticks > 0 ? $"{time.Ticks} ticks" : "0";
    }
}