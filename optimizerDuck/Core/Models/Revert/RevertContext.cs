using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.Models.Revert;

public sealed class RevertContext : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly RevertManager _manager;
    private readonly IOptimization _optimization;
    private readonly Stack<IRevertStep> _steps = new();

    internal RevertContext(IOptimization optimization, ILogger logger, RevertManager manager)
    {
        _optimization = optimization;
        _logger = logger;
        _manager = manager;
    }

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

    internal void AddStep(IRevertStep step)
    {
        _steps.Push(step);
    }
}