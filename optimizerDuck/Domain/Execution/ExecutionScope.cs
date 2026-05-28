using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Domain.Execution;

public sealed class ExecutionScope : IDisposable
{
    private static readonly AsyncLocal<ExecutionScope?> _current = new();
    private readonly List<ExecutedStep> _executedSteps = [];
    private readonly ConcurrentDictionary<string, Stats> _stats = new();

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _disposed;
    private int _stepIndex;

    /// <summary>Gets the current <see cref="ExecutionScope"/> for the ambient async context.</summary>
    /// <remarks>
    /// Backed by <see cref="AsyncLocal{T}"/>, this flows across async calls without requiring
    /// dependency injection. Provider services read <see cref="Current"/> to record revert steps.
    /// </remarks>
    public static ExecutionScope? Current => _current.Value;

    /// <summary>Gets the identifier of the optimization being executed.</summary>
    public Guid? OptimizationId { get; private init; }

    /// <summary>Gets the unique key of the optimization being executed.</summary>
    public string? OptimizationKey { get; private init; }

    /// <summary>Gets the display name of the optimization being executed.</summary>
    public string? OptimizationName { get; init; }

    /// <summary>Gets the logger instance for the current execution scope.</summary>
    public required ILogger Logger { get; init; }

    /// <summary>Gets all recorded execution steps in the order they were executed.</summary>
    /// <value>An <see cref="IReadOnlyList{T}"/> of <see cref="ExecutedStep"/> instances.</value>
    public IReadOnlyList<ExecutedStep> ExecutedSteps => _executedSteps.AsReadOnly();

    /// <summary>Gets the subset of steps that completed successfully.</summary>
    /// <value>An array of <see cref="ExecutedStep"/> instances where <see cref="ExecutedStep.Success"/> is <see langword="true"/>.</value>
    public IReadOnlyList<ExecutedStep> SuccessfulSteps =>
        _executedSteps.Where(s => s.Success).ToArray();

    /// <summary>Gets the subset of steps that failed.</summary>
    /// <value>An array of <see cref="ExecutedStep"/> instances where <see cref="ExecutedStep.Success"/> is <see langword="false"/>.</value>
    public IReadOnlyList<ExecutedStep> FailedSteps =>
        _executedSteps.Where(s => !s.Success).ToArray();

    /// <summary>Gets a value that indicates whether at least one step succeeded.</summary>
    /// <value><see langword="true"/> if any recorded step completed successfully; otherwise, <see langword="false"/>.</value>
    public bool HasSuccessfulSteps => _executedSteps.Any(s => s.Success);

    private bool LoggingOnly { get; init; }

    /// <summary>
    /// Releases the execution scope, logs execution statistics, and persists revert data
    /// if the scope is associated with an optimization that has successful steps.
    /// </summary>
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
                _stopwatch.Elapsed.ToString(@"mm\:ss\.fff")
            );
        }
        else
        {
            var summary = string.Join(
                ", ",
                _stats.Select(kv => $"{kv.Key}: +({kv.Value.Success}) -({kv.Value.Fail})")
            );

            Logger.LogInformation(
                "Completed in {Time} | {Summary}",
                _stopwatch.Elapsed.ToString(@"mm\:ss\.fff"),
                summary
            );
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

    /// <summary>Creates a new execution scope for an optimization and sets it as the ambient scope.</summary>
    /// <param name="optimization">The optimization to execute.</param>
    /// <param name="logger">The logger instance for the scope.</param>
    /// <returns>A new <see cref="ExecutionScope"/> instance.</returns>
    /// <exception cref="InvalidOperationException">An execution scope is already active in the current async context.</exception>
    public static ExecutionScope Begin(IOptimization optimization, ILogger logger)
    {
        if (_current.Value != null)
            throw new InvalidOperationException(
                "An execution scope is already active in the current context."
            );

        var scope = new ExecutionScope
        {
            OptimizationId = optimization.Id,
            OptimizationKey = optimization.OptimizationKey,
            OptimizationName = optimization.Name,
            Logger = logger,
        };
        _current.Value = scope;
        logger.LogDebug(
            "Execution scope started for {Key} (ID: {Id})",
            optimization.OptimizationKey,
            optimization.Id
        );
        return scope;
    }

    /// <summary>Creates a logging-only scope that records steps but does not persist revert data.</summary>
    /// <param name="logger">The logger instance for the scope.</param>
    /// <returns>A new <see cref="ExecutionScope"/> instance with <see cref="LoggingOnly"/> set to <see langword="true"/>.</returns>
    /// <exception cref="InvalidOperationException">An execution scope is already active in the current async context.</exception>
    public static ExecutionScope BeginForLogging(ILogger logger)
    {
        if (_current.Value != null)
            throw new InvalidOperationException(
                "An execution scope is already active in the current context."
            );

        var scope = new ExecutionScope
        {
            OptimizationId = Guid.Empty,
            OptimizationKey = string.Empty,
            OptimizationName = string.Empty,
            Logger = logger,
            LoggingOnly = true,
        };
        _current.Value = scope;
        logger.LogDebug("Execution scope started (logging only)");
        return scope;
    }

    /// <summary>Creates a logging-only scope scoped to a specific optimization for diagnostic purposes.</summary>
    /// <param name="optimizationId">The identifier of the optimization.</param>
    /// <param name="optimizationKey">The unique key of the optimization.</param>
    /// <param name="logger">The logger instance for the scope.</param>
    /// <returns>A new <see cref="ExecutionScope"/> instance with <see cref="LoggingOnly"/> set to <see langword="true"/>.</returns>
    /// <exception cref="InvalidOperationException">An execution scope is already active in the current async context.</exception>
    public static ExecutionScope BeginForLogging(
        Guid optimizationId,
        string optimizationKey,
        ILogger logger
    )
    {
        if (_current.Value != null)
            throw new InvalidOperationException(
                "An execution scope is already active in the current context."
            );

        var scope = new ExecutionScope
        {
            OptimizationId = optimizationId,
            OptimizationKey = optimizationKey,
            OptimizationName = string.Empty,
            Logger = logger,
            LoggingOnly = true,
        };
        _current.Value = scope;
        logger.LogDebug("Execution scope started for {Key} (logging only)", optimizationKey);
        return scope;
    }

    /// <summary>Creates a temporary scope used to re-capture revert steps during retry execution.</summary>
    /// <remarks>
    /// During retry, the original optimization context is not available, so
    /// <see cref="OptimizationId"/> is set to <see cref="Guid.Empty"/>. Captured steps are later
    /// re-assigned to the real scope via <see cref="RecordStepAtIndex"/>.
    /// </remarks>
    /// <param name="logger">The logger instance for the scope.</param>
    /// <returns>A new <see cref="ExecutionScope"/> instance with an empty optimization context.</returns>
    /// <exception cref="InvalidOperationException">An execution scope is already active in the current async context.</exception>
    public static ExecutionScope BeginForCapture(ILogger logger)
    {
        if (_current.Value != null)
            throw new InvalidOperationException(
                "An execution scope is already active in the current context."
            );

        var scope = new ExecutionScope
        {
            OptimizationId = Guid.Empty,
            OptimizationKey = string.Empty,
            OptimizationName = string.Empty,
            Logger = logger,
        };
        _current.Value = scope;
        logger.LogDebug("Execution scope started for retry capture");
        return scope;
    }

    /// <summary>Records an execution step onto the ambient scope with auto-incremented index.</summary>
    /// <param name="name">The category name of the step (for example, "Registry", "Shell").</param>
    /// <param name="description">A human-readable description of what the step does.</param>
    /// <param name="success"><see langword="true"/> if the step completed successfully; otherwise, <see langword="false"/>.</param>
    /// <param name="revertStep">An optional <see cref="IRevertStep"/> that can undo this operation.</param>
    /// <param name="error">An optional error message that describes the failure.</param>
    /// <param name="retryAction">An optional asynchronous action that retries the failed step.</param>
    /// <param name="errorDetail">An optional detailed error string (for example, exception details).</param>
    /// <returns>The recorded <see cref="ExecutedStep"/>, or <see langword="null"/> if no ambient scope exists or the scope is logging-only.</returns>
    public static ExecutedStep? RecordStep(
        string name,
        string description,
        bool success,
        IRevertStep? revertStep = null,
        string? error = null,
        Func<Task<bool>>? retryAction = null,
        string? errorDetail = null
    )
    {
        var scope = Current;

        return scope?.RecordStepInternal(
            name,
            description,
            success,
            revertStep,
            error,
            retryAction,
            errorDetail
        );
    }

    /// <summary>Records an execution step at a specific index, used during retry to preserve the original index layout.</summary>
    /// <param name="index">The explicit 1-based index for the step.</param>
    /// <param name="name">The category name of the step (for example, "Registry", "Shell").</param>
    /// <param name="description">A human-readable description of what the step does.</param>
    /// <param name="success"><see langword="true"/> if the step completed successfully; otherwise, <see langword="false"/>.</param>
    /// <param name="revertStep">An optional <see cref="IRevertStep"/> that can undo this operation.</param>
    /// <param name="error">An optional error message that describes the failure.</param>
    /// <param name="retryAction">An optional asynchronous action that retries the failed step.</param>
    /// <param name="errorDetail">An optional detailed error string (for example, exception details).</param>
    /// <returns>The recorded <see cref="ExecutedStep"/>, or <see langword="null"/> if no ambient scope exists or the scope is logging-only.</returns>
    public static ExecutedStep? RecordStepAtIndex(
        int index,
        string name,
        string description,
        bool success,
        IRevertStep? revertStep = null,
        string? error = null,
        Func<Task<bool>>? retryAction = null,
        string? errorDetail = null
    )
    {
        var scope = Current;

        return scope?.RecordStepInternal(
            name,
            description,
            success,
            revertStep,
            error,
            retryAction,
            errorDetail,
            explicitIndex: index
        );
    }

    /// <summary>Records a service invocation in the execution statistics.</summary>
    /// <param name="serviceName">The name of the service that was invoked.</param>
    /// <param name="success"><see langword="true"/> if the service call succeeded; otherwise, <see langword="false"/>.</param>
    public static void Track(string serviceName, bool success)
    {
        var scope = Current;

        scope?._stats.AddOrUpdate(
            serviceName,
            _ => success ? new Stats { Success = 1 } : new Stats { Fail = 1 },
            (_, stats) =>
            {
                if (success)
                    stats.Success++;
                else
                    stats.Fail++;
                return stats;
            }
        );
    }

    /// <summary>Returns the list of step results for display in the UI.</summary>
    /// <returns>A <see cref="List{T}"/> of <see cref="OperationStepResult"/> instances.</returns>
    public List<OperationStepResult> GetStepResults()
    {
        return _executedSteps.Select(ToOperationStepResult).ToList();
    }

    /// <summary>Maps the execution scope to an <see cref="ApplyResult"/> from the recorded steps.</summary>
    /// <returns><see cref="ApplyResult.True"/> if every step succeeded or some succeeded (partial success); <see cref="ApplyResult.False"/> with a message if all steps failed.</returns>
    public ApplyResult ToApplyResult()
    {
        var result = ToResult();
        return
            result.Status
                is OptimizationSuccessResult.Success
                    or OptimizationSuccessResult.PartialSuccess
            ? ApplyResult.True()
            : ApplyResult.False(result.Message);
    }

    /// <summary>Builds an <see cref="OptimizationResult"/> from the recorded steps.</summary>
    /// <returns>
    /// An <see cref="OptimizationResult"/> whose <see cref="OptimizationResult.Status"/> reflects whether
    /// all steps succeeded, some failed (partial), or all failed.
    /// </returns>
    public OptimizationResult ToResult()
    {
        var status = ResolveStatus();
        var failedSteps = FailedSteps.Select(ToOperationStepResult).ToList();

        var allFailed = failedSteps.Count == ExecutedSteps.Count;

        return new OptimizationResult
        {
            Status = status,
            Message = status switch
            {
                OptimizationSuccessResult.Success => string.Format(
                    Translations.Optimization_Apply_Success,
                    OptimizationName
                ),
                OptimizationSuccessResult.Failed when allFailed => string.Format(
                    Translations.Optimization_Apply_Error_Failed,
                    OptimizationName
                ),
                OptimizationSuccessResult.PartialSuccess or OptimizationSuccessResult.Failed =>
                    string.Format(
                        Translations.Optimization_Apply_Error_FailedWithSteps,
                        OptimizationName,
                        failedSteps.Count
                    ),
                _ => string.Format(Translations.Optimization_Apply_Error_Unknown, OptimizationName),
            },
            FailedSteps = failedSteps,
        };
    }

    private ExecutedStep? RecordStepInternal(
        string name,
        string description,
        bool success,
        IRevertStep? revertStep,
        string? error,
        Func<Task<bool>>? retryAction = null,
        string? errorDetail = null,
        int? explicitIndex = null
    )
    {
        if (LoggingOnly)
            return null;

        // explicitIndex is used during retry to re-record a step at its original position
        // so the gap-and-index layout in the revert file stays consistent
        var stepIndex = explicitIndex ?? ++_stepIndex;

        var step = new ExecutedStep(
            stepIndex,
            name,
            description,
            success,
            revertStep,
            error,
            retryAction,
            errorDetail
        );

        _executedSteps.Add(step);

        return step;
    }

    private OptimizationSuccessResult ResolveStatus()
    {
        if (_executedSteps.Count == 0)
            return OptimizationSuccessResult.Failed;

        var failed = FailedSteps.Count;
        var total = ExecutedSteps.Count;

        return failed == 0 ? OptimizationSuccessResult.Success
            : failed < total ? OptimizationSuccessResult.PartialSuccess
            : OptimizationSuccessResult.Failed;
    }

    private static OperationStepResult ToOperationStepResult(ExecutedStep step)
    {
        return new OperationStepResult
        {
            Index = step.Index,
            Name = step.Name,
            Description = step.Description,
            Success = step.Success,
            Error = step.Error,
            ErrorDetail = step.ErrorDetail,
            RetryAction = step.RetryAction,
            RevertStep = step.RevertStep,
        };
    }

    private class Stats
    {
        public int Fail;
        public int Success;
    }

    #region Logging

    /// <summary>Logs a message at the specified level through the ambient scope's logger.</summary>
    /// <param name="level">The log level.</param>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional format arguments.</param>
    public static void Log(LogLevel level, string message, params object[] args)
    {
        Current?.Logger.Log(level, message, args);
    }

    /// <summary>Logs a debug-level message.</summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional format arguments.</param>
    public static void LogDebug(string message, params object[] args)
    {
        Log(LogLevel.Debug, message, args);
    }

    /// <summary>Logs a trace-level message.</summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional format arguments.</param>
    public static void LogTrace(string message, params object[] args)
    {
        Log(LogLevel.Trace, message, args);
    }

    /// <summary>Logs an informational message.</summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional format arguments.</param>
    public static void LogInfo(string message, params object[] args)
    {
        Log(LogLevel.Information, message, args);
    }

    /// <summary>Logs a warning message.</summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional format arguments.</param>
    public static void LogWarning(string message, params object[] args)
    {
        Log(LogLevel.Warning, message, args);
    }

    /// <summary>Logs an error message with an optional exception.</summary>
    /// <param name="ex">An optional exception that caused the error.</param>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional format arguments.</param>
    public static void LogError(Exception? ex, string message, params object[] args)
    {
        Current?.Logger.LogError(ex, message, args);
    }

    #endregion Logging
}

/// <summary>Represents a single step recorded during optimization execution.</summary>
public sealed record ExecutedStep
{
    /// <summary>Initializes a new instance of the <see cref="ExecutedStep"/> class.</summary>
    /// <param name="index">The 1-based index of the step in the execution sequence.</param>
    /// <param name="name">The category name of the step (for example, "Registry", "Shell").</param>
    /// <param name="description">A human-readable description of what the step did.</param>
    /// <param name="success"><see langword="true"/> if the step completed successfully; otherwise, <see langword="false"/>.</param>
    /// <param name="revertStep">An <see cref="IRevertStep"/> that can undo this operation, if applicable.</param>
    /// <param name="error">An error message if the step failed, or <see langword="null"/>.</param>
    /// <param name="retryAction">An optional asynchronous action to retry the step on failure.</param>
    /// <param name="errorDetail">An optional detailed error string (for example, exception details).</param>
    public ExecutedStep(
        int index,
        string name,
        string description,
        bool success,
        IRevertStep? revertStep,
        string? error,
        Func<Task<bool>>? retryAction = null,
        string? errorDetail = null
    )
    {
        Index = index;
        Name = name;
        Description = description;
        Success = success;
        RevertStep = revertStep;
        Error = error;
        RetryAction = retryAction;
        ErrorDetail = errorDetail;
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
    ///     Detailed error information (exception details) for the step failure.
    /// </summary>
    public string? ErrorDetail { get; init; }

    /// <summary>
    ///     An optional action to retry this step if it failed.
    /// </summary>
    public Func<Task<bool>>? RetryAction { get; init; }
}
