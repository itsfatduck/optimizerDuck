using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Core.Models.Optimization;

namespace optimizerDuck.Services;

public sealed class ServiceTracker : IDisposable
{
    private static readonly AsyncLocal<ServiceTracker?> _current = new();

    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Stats> _stats = new();
    private readonly List<OperationStepResult> _steps = new();
    private readonly Stopwatch _stopwatch;
    private int _stepIndex;

    private ServiceTracker(ILogger logger)
    {
        _logger = logger;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>Current active tracker (null if none)</summary>
    public static ServiceTracker? Current => _current.Value;

    public void Dispose()
    {
        _stopwatch.Stop();

        if (_stats.IsEmpty)
        {
            _logger.LogInformation("Completed in {Time} (no operations tracked)",
                _stopwatch.Elapsed.FormatTime());
        }
        else
        {
            var summary = string.Join(", ", _stats.Select(kv =>
                $"{kv.Key}: +({kv.Value.Success}) -({kv.Value.Fail})"));

            _logger.LogInformation("Completed in {Time} | {Summary}",
                _stopwatch.Elapsed.FormatTime(), summary);
        }

        _current.Value = null;
    }

    /// <summary>Begin tracking with a logger</summary>
    public static ServiceTracker Begin(ILogger logger)
    {
        var tracker = new ServiceTracker(logger);
        _current.Value = tracker;
        return tracker;
    }

    /// <summary>Track a service operation result</summary>
    public static void Track(string serviceName, bool success)
    {
        var tracker = Current;

        tracker?._stats.AddOrUpdate(
            serviceName,
            _ => success
                ? new Stats { Success = 1 }
                : new Stats { Fail = 1 },
            (_, stats) =>
            {
                if (success) stats.Success++;
                else stats.Fail++;
                return stats;
            });
    }


    public static void TrackStep(string name, string description, bool success, string? error = null,
        Func<Task<bool>>? retryAction = null)
    {
        var tracker = Current;
        if (tracker == null)
            return;

        var index = ++tracker._stepIndex;
        tracker._steps.Add(new OperationStepResult
        {
            Index = index,
            Name = name,
            Description = description,
            Success = success,
            Error = error,
            RetryAction = retryAction
        });
    }

    public IReadOnlyList<OperationStepResult> GetSteps()
    {
        return _steps.ToList();
    }

    /// <summary>Log a message using the current tracker's logger</summary>
    public static void Log(LogLevel level, string message, params object[] args)
    {
        Current?._logger.Log(level, message, args);
    }

    /// <summary>Log debug message</summary>
    public static void LogDebug(string message, params object[] args)
    {
        Log(LogLevel.Debug, message, args);
    }

    /// <summary>Log trace message</summary>
    public static void LogTrace(string message, params object[] args)
    {
        Log(LogLevel.Trace, message, args);
    }

    /// <summary>Log info message</summary>
    public static void LogInfo(string message, params object[] args)
    {
        Log(LogLevel.Information, message, args);
    }

    /// <summary>Log warning message</summary>
    public static void LogWarning(string message, params object[] args)
    {
        Log(LogLevel.Warning, message, args);
    }

    /// <summary>Log error message</summary>
    public static void LogError(Exception? ex, string message, params object[] args)
    {
        Current?._logger.LogError(ex, message, args);
    }

    private class Stats
    {
        public int Fail;
        public int Success;
    }
}