using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Core.Models.Execution;

public sealed class ExecutionScope : IDisposable
{
    private static readonly AsyncLocal<ExecutionScope?> _current = new();
    private readonly List<ExecutedStep> _executedSteps = [];
    private readonly ConcurrentDictionary<string, Stats> _stats = new();

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _disposed;
    private int _stepIndex;


    public static ExecutionScope? Current => _current.Value;

    public Guid? OptimizationId { get; private init; }

    public string? OptimizationKey { get; private init; }

    public string? OptimizationName { get; init; }

    public required ILogger Logger { get; init; }

    public IReadOnlyList<ExecutedStep> ExecutedSteps => _executedSteps.AsReadOnly();

    public IReadOnlyList<ExecutedStep> SuccessfulSteps =>
        _executedSteps.Where(s => s.Success).ToArray();

    public IReadOnlyList<ExecutedStep> FailedSteps =>
        _executedSteps.Where(s => !s.Success).ToArray();

    public bool HasSuccessfulSteps => _executedSteps.Any(s => s.Success);

    private bool LoggingOnly { get; init; }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _stopwatch.Stop();

        if (_stats.IsEmpty)
        {
            Logger.LogInformation(
                "Completed in {Time} (no operations tracked)",
                _stopwatch.Elapsed.ToString(@"mm\:ss\.fff"));
        }
        else
        {
            var summary = string.Join(", ", _stats.Select(kv =>
                $"{kv.Key}: +({kv.Value.Success}) -({kv.Value.Fail})"));

            Logger.LogInformation(
                "Completed in {Time} | {Summary}",
                _stopwatch.Elapsed.ToString(@"mm\:ss\.fff"),
                summary);
        }

        if (!LoggingOnly)
            Logger.LogInformation(
                "Steps: {Total} ({Success} success, {Failed} failed)",
                _executedSteps.Count,
                SuccessfulSteps.Count,
                FailedSteps.Count
            );

        _current.Value = null;
    }

    public static ExecutionScope Begin(IOptimization optimization, ILogger logger)
    {
        if (_current.Value != null)
            throw new InvalidOperationException("An execution scope is already active in the current context.");

        var scope = new ExecutionScope
        {
            OptimizationId = optimization.Id, OptimizationKey = optimization.OptimizationKey,
            OptimizationName = optimization.Name, Logger = logger
        };
        _current.Value = scope;
        logger.LogDebug("Execution scope started for {Key} (ID: {Id})", optimization.OptimizationKey, optimization.Id);
        return scope;
    }

    public static ExecutionScope BeginForLogging(ILogger logger)
    {
        if (_current.Value != null)
            throw new InvalidOperationException("An execution scope is already active in the current context.");

        var scope = new ExecutionScope
        {
            OptimizationId = Guid.Empty, OptimizationKey = string.Empty, OptimizationName = string.Empty,
            Logger = logger, LoggingOnly = true
        };
        _current.Value = scope;
        logger.LogDebug("Execution scope started (logging only)");
        return scope;
    }

    public static ExecutionScope BeginForLogging(Guid optimizationId, string optimizationKey, ILogger logger)
    {
        if (_current.Value != null)
            throw new InvalidOperationException("An execution scope is already active in the current context.");

        var scope = new ExecutionScope
        {
            OptimizationId = optimizationId, OptimizationKey = optimizationKey, OptimizationName = string.Empty,
            Logger = logger, LoggingOnly = true
        };
        _current.Value = scope;
        logger.LogDebug("Execution scope started for {Key} (logging only)", optimizationKey);
        return scope;
    }

    public static ExecutedStep? RecordStep(
        string name,
        string description,
        bool success,
        IRevertStep? revertStep = null,
        string? error = null,
        Func<Task<bool>>? retryAction = null)
    {
        var scope = Current;

        return scope?.RecordStepInternal(name, description, success, revertStep, error, retryAction);
    }

    public static void Track(string serviceName, bool success)
    {
        var scope = Current;

        scope?._stats.AddOrUpdate(
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

    public List<OperationStepResult> GetStepResults()
    {
        return _executedSteps.Select(s => new OperationStepResult
        {
            Index = s.Index,
            Name = s.Name,
            Description = s.Description,
            Success = s.Success,
            Error = s.Error,
            RetryAction = s.RetryAction,
            RevertStep = s.RevertStep
        }).ToList();
    }

    public OptimizationResult ToResult()
    {
        var status = ResolveStatus();
        var failedSteps = FailedSteps.Select(s => new OperationStepResult
        {
            Index = s.Index,
            Name = s.Name,
            Description = s.Description,
            Success = s.Success,
            Error = s.Error,
            RetryAction = s.RetryAction,
            RevertStep = s.RevertStep
        }).ToList();

        var allFailed = failedSteps.Count == ExecutedSteps.Count;

        return new OptimizationResult
        {
            Status = status,
            Message = status switch
            {
                OptimizationSuccessResult.Success =>
                    string.Format(Translations.Optimization_Apply_Success,
                        OptimizationName),
                OptimizationSuccessResult.Failed when allFailed =>
                    string.Format(Translations.Optimization_Apply_Error_Failed,
                        OptimizationName),
                OptimizationSuccessResult.PartialSuccess or OptimizationSuccessResult.Failed =>
                    string.Format(
                        Translations.Optimization_Apply_Error_FailedWithSteps,
                        OptimizationName,
                        failedSteps.Count),
                _ => $"Unknown result for {OptimizationName}"
            },
            FailedSteps = failedSteps
        };
    }

    private ExecutedStep? RecordStepInternal(
        string name,
        string description,
        bool success,
        IRevertStep? revertStep,
        string? error,
        Func<Task<bool>>? retryAction = null)
    {
        if (LoggingOnly)
            return null;

        var step = new ExecutedStep(
            ++_stepIndex,
            name,
            description,
            success,
            revertStep,
            error,
            retryAction);

        _executedSteps.Add(step);

        return step;
    }

    private OptimizationSuccessResult ResolveStatus()
    {
        if (_executedSteps.Count == 0)
            return OptimizationSuccessResult.Failed;

        var failed = FailedSteps.Count;
        var total = ExecutedSteps.Count;

        return failed == 0
            ? OptimizationSuccessResult.Success
            : failed < total
                ? OptimizationSuccessResult.PartialSuccess
                : OptimizationSuccessResult.Failed;
    }

    private class Stats
    {
        public int Fail;
        public int Success;
    }

    #region Logging

    public static void Log(LogLevel level, string message, params object[] args)
    {
        Current?.Logger.Log(level, message, args);
    }

    public static void LogDebug(string message, params object[] args)
    {
        Log(LogLevel.Debug, message, args);
    }

    public static void LogTrace(string message, params object[] args)
    {
        Log(LogLevel.Trace, message, args);
    }

    public static void LogInfo(string message, params object[] args)
    {
        Log(LogLevel.Information, message, args);
    }

    public static void LogWarning(string message, params object[] args)
    {
        Log(LogLevel.Warning, message, args);
    }

    public static void LogError(Exception? ex, string message, params object[] args)
    {
        Current?.Logger.LogError(ex, message, args);
    }

    #endregion Logging
}

/// <summary>
///     Represents a single step recorded during optimization execution.
/// </summary>
public sealed record ExecutedStep
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExecutedStep" /> class.
    /// </summary>
    public ExecutedStep(int index, string name, string description, bool success,
        IRevertStep? revertStep, string? error, Func<Task<bool>>? retryAction = null)
    {
        Index = index;
        Name = name;
        Description = description;
        Success = success;
        RevertStep = revertStep;
        Error = error;
        RetryAction = retryAction;
    }

    /// <summary>
    ///     The index of the step in the execution sequence.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    ///     The category/name of the step (e.g., "Registry", "Shell").
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    ///     A description of what the step did.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    ///     Whether the step succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    ///     The revert step that can undo this operation, if applicable.
    /// </summary>
    public IRevertStep? RevertStep { get; init; }

    /// <summary>
    ///     The error message if the step failed, or null if it succeeded.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    ///     An optional action to retry this step if it failed.
    /// </summary>
    public Func<Task<bool>>? RetryAction { get; init; }
}