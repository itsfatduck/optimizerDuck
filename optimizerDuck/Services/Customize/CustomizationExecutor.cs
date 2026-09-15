using Microsoft.Extensions.Logging;

namespace optimizerDuck.Services.Customize;

/// <summary>
///     Serializes customization writes behind a debounce, so a burst of setting changes
///     applies only the last value.
/// </summary>
public sealed class CustomizationExecutor(ILogger<CustomizationExecutor>? logger = null)
    : IDisposable
{
    private readonly Lock _gate = new();
    private object? _pendingValue;
    private bool _hasPendingValue;
    private bool _isApplying;
    private CancellationTokenSource? _debounceCts;

    /// <summary>Gets a value that indicates whether a queued value is being applied.</summary>
    public bool IsApplying
    {
        get
        {
            lock (_gate)
                return _isApplying;
        }
    }

    /// <summary>
    ///     Applies a value once the debounce elapses, one queued apply at a time.
    /// </summary>
    public async Task ApplyWithDebounceAsync(
        object? value,
        Func<object?, Task> applyAction,
        int debounceMs = 400
    )
    {
        CancellationToken token;
        lock (_gate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            token = _debounceCts.Token;
        }

        try
        {
            await Task.Delay(debounceMs, token);
            if (token.IsCancellationRequested)
                return;

            QueueApply(value, applyAction);
        }
        catch (TaskCanceledException) { }
    }

    private void QueueApply(object? value, Func<object?, Task> applyAction)
    {
        lock (_gate)
        {
            _pendingValue = value;
            _hasPendingValue = true;

            if (_isApplying)
                return;

            _isApplying = true;
        }

        _ = ProcessQueueAsync(applyAction);
    }

    private async Task ProcessQueueAsync(Func<object?, Task> applyAction)
    {
        while (true)
        {
            object? valueToApply;
            lock (_gate)
            {
                if (!_hasPendingValue)
                {
                    _isApplying = false;
                    break;
                }

                valueToApply = _pendingValue;
                _hasPendingValue = false;
            }

            try
            {
                await applyAction(valueToApply);
            }
            catch (Exception ex)
            {
                // Fire-and-forget: a throw would go unobserved, so failures are logged.
                logger?.LogError(ex, "Customization apply failed.");
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
    }
}
