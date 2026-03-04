using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.Models.Revert;

/// <summary>
///     Manages the recording of revert steps during an optimization apply operation.
///     Implements <see cref="IAsyncDisposable" /> to automatically persist recorded steps on disposal.
/// </summary>
public sealed class RevertContext : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly RevertManager _manager;
    private readonly IOptimization _optimization;
    private readonly Stack<IRevertStep> _steps = new();

    /// <summary>
    ///     Initializes a new <see cref="RevertContext" /> for the given optimization.
    /// </summary>
    /// <param name="optimization">The optimization being performed.</param>
    /// <param name="logger">The logger for diagnostic messages.</param>
    /// <param name="manager">The revert manager responsible for persistence.</param>
    internal RevertContext(IOptimization optimization, ILogger logger, RevertManager manager)
    {
        _optimization = optimization;
        _logger = logger;
        _manager = manager;
    }

    /// <summary>
    ///     Persists recorded revert steps and clears the current context.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_steps.Count > 0)
                await _manager.SaveAsync(
                    _optimization.Id,
                    _optimization.OptimizationKey,
                    _steps);
            else
                _logger.LogWarning("No revert steps recorded for {Name}", _optimization.OptimizationKey);
        }
        finally
        {
            if (RevertManager.Current == this) RevertManager.ClearCurrent();
        }
    }

    /// <summary>
    ///     Adds a revert step to the recording stack.
    /// </summary>
    /// <param name="step">The revert step to record.</param>
    internal void AddStep(IRevertStep step)
    {
        _steps.Push(step);
    }
}