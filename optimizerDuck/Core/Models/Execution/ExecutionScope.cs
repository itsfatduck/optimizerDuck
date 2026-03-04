using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.UI;

namespace optimizerDuck.Core.Models.Execution;

public sealed class ExecutionScope : IDisposable
{
    private static readonly AsyncLocal<ExecutionScope?> _current = new();

    private readonly Guid _optimizationId;
    private readonly string _optimizationKey;
    private readonly ILogger _logger;
    private readonly Stopwatch _stopwatch;
    private readonly List<ExecutedStep> _executedSteps = [];
    private readonly ConcurrentDictionary<string, Stats> _stats = new();
    private int _stepIndex;
    private bool _disposed;

    private ExecutionScope(Guid optimizationId, string optimizationKey, ILogger logger)
    {
        _optimizationId = optimizationId;
        _optimizationKey = optimizationKey;
        _logger = logger;
        _stopwatch = Stopwatch.StartNew();
    }

    public static ExecutionScope? Current => _current.Value;

    public Guid OptimizationId => _optimizationId;

    public string OptimizationKey => _optimizationKey;

    public ILogger Logger => _logger;

    public IReadOnlyList<ExecutedStep> ExecutedSteps => _executedSteps.AsReadOnly();

    public IReadOnlyList<ExecutedStep> SuccessfulSteps =>
        _executedSteps.Where(s => s.Success).ToArray();

    public IReadOnlyList<ExecutedStep> FailedSteps =>
        _executedSteps.Where(s => !s.Success).ToArray();

    public bool HasSuccessfulSteps => _executedSteps.Any(s => s.Success);

    public static ExecutionScope Begin(Guid optimizationId, string optimizationKey, ILogger logger)
    {
        if (_current.Value != null)
            throw new InvalidOperationException("An execution scope is already active in the current context.");

        var scope = new ExecutionScope(optimizationId, optimizationKey, logger);
        _current.Value = scope;
        logger.LogDebug("Execution scope started for {Key} (ID: {Id})", optimizationKey, optimizationId);
        return scope;
    }

    public static ExecutionScope Begin(ILogger logger)
    {
        if (_current.Value != null)
            throw new InvalidOperationException("An execution scope is already active in the current context.");

        var scope = new ExecutionScope(Guid.Empty, string.Empty, logger);
        _current.Value = scope;
        logger.LogDebug("Execution scope started");
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
        if (scope == null)
            return null;

        return scope.RecordStepInternal(name, description, success, revertStep, error, retryAction);
    }

    public static void Track(string serviceName, bool success)
    {
        var scope = Current;
        if (scope == null)
            return;

        scope._stats.AddOrUpdate(
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
            RetryAction = s.RetryAction
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
            RetryAction = s.RetryAction
        }).ToList();

        var allFailed = failedSteps.Count == ExecutedSteps.Count;
        var hasFailures = failedSteps.Count > 0;

        return new OptimizationResult
        {
            Status = status,
            Message = status switch
            {
                OptimizationSuccessResult.Success =>
                    string.Format(optimizerDuck.Resources.Languages.Translations.Optimization_Apply_Success,
                        _optimizationKey),
                OptimizationSuccessResult.Failed when allFailed =>
                    string.Format(optimizerDuck.Resources.Languages.Translations.Optimization_Apply_Error_Failed,
                        _optimizationKey),
                OptimizationSuccessResult.PartialSuccess or OptimizationSuccessResult.Failed =>
                    string.Format(
                        optimizerDuck.Resources.Languages.Translations.Optimization_Apply_Error_FailedWithSteps,
                        _optimizationKey,
                        failedSteps.Count),
                _ => $"Unknown result for {_optimizationKey}"
            },
            FailedSteps = failedSteps
        };
    }

    private ExecutedStep RecordStepInternal(
        string name,
        string description,
        bool success,
        IRevertStep? revertStep,
        string? error,
        Func<Task<bool>>? retryAction = null)
    {
        var step = new ExecutedStep(
            ++_stepIndex,
            name,
            description,
            success,
            revertStep,
            error,
            retryAction);

        _executedSteps.Add(step);

        _logger.LogDebug("Step {Index} ({Name}): {Result}",
            step.Index, name, success ? "SUCCESS" : "FAILED");

        return step;
    }

    #region Logging

    public static void Log(LogLevel level, string message, params object[] args)
    {
        Current?._logger.Log(level, message, args);
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
        Current?._logger.LogError(ex, message, args);
    }

    #endregion Logging

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

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _stopwatch.Stop();

        if (_stats.IsEmpty)
        {
            _logger.LogInformation(
                "[{Key}] Completed in {Time} (no operations tracked)",
                _optimizationKey,
                _stopwatch.Elapsed.ToString(@"mm\:ss\.fff"));
        }
        else
        {
            var summary = string.Join(", ", _stats.Select(kv =>
                $"{kv.Key}: +({kv.Value.Success}) -({kv.Value.Fail})"));

            _logger.LogInformation(
                "[{Key}] Completed in {Time} | {Summary}",
                _optimizationKey,
                _stopwatch.Elapsed.ToString(@"mm\:ss\.fff"),
                summary);
        }

        _logger.LogInformation(
            "[{Key}] Steps: {Total} ({Success} success, {Failed} failed)",
            _optimizationKey,
            _executedSteps.Count,
            _executedSteps.Count(s => s.Success),
            _executedSteps.Count(s => !s.Success)
        );

        _current.Value = null;
    }

    private class Stats
    {
        public int Success;
        public int Fail;
    }
}

/// <summary>
///     Represents a step that was executed during an optimization.
/// </summary>
public sealed record ExecutedStep
{
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
}
