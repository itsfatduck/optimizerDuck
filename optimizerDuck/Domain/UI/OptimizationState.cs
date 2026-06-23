using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Domain.UI;

/// <summary>
///     Tracks the applied state and timing of an optimization.
///     Provides relative time display (e.g., "Applied 5 minutes ago").
/// </summary>
public partial class OptimizationState : ObservableObject
{
    private static DispatcherTimer? _globalTimer;
    private static readonly List<WeakReference<OptimizationState>> _instances = [];
    private static readonly object _lock = new();

    private int _lastDisplayedSeconds = -1;

    /// <summary>
    ///     The date and time when the optimization was applied.
    /// </summary>
    [ObservableProperty]
    private DateTime? appliedAt;

    /// <summary>
    ///     Indicates whether the optimization is currently applied.
    /// </summary>
    [ObservableProperty]
    private bool isApplied;

    /// <summary>
    ///     A human-readable relative time string (e.g., "5 minutes ago").
    /// </summary>
    [ObservableProperty]
    private string? relativeTime = string.Empty;

    /// <summary>
    ///     The risk level of the optimization.
    /// </summary>
    [ObservableProperty]
    private OptimizationRisk risk;

    public OptimizationState()
    {
        EnsureTimerRunning();
        lock (_lock)
        {
            _instances.Add(new WeakReference<OptimizationState>(this));
        }
    }

    private static void EnsureTimerRunning()
    {
        if (_globalTimer != null)
            return;

        // Defer timer creation to avoid accessing dispatcher during static init
        _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            lock (_lock)
            {
                if (_globalTimer != null)
                    return;
                _globalTimer = new DispatcherTimer(
                    TimeSpan.FromSeconds(1),
                    DispatcherPriority.Render,
                    (s, e) => UpdateAllRelativeTimes(),
                    System.Windows.Application.Current.Dispatcher
                );
                _globalTimer.Start();
            }
        });
    }

    private static void UpdateAllRelativeTimes()
    {
        lock (_lock)
        {
            for (var i = _instances.Count - 1; i >= 0; i--)
                if (_instances[i].TryGetTarget(out var instance))
                    instance.UpdateRelativeTime();
                else
                    _instances.RemoveAt(i);

            // Stop the timer when no live instances remain
            if (_instances.Count == 0 && _globalTimer != null)
            {
                _globalTimer.Stop();
                _globalTimer = null;
            }
        }
    }

    private void UpdateRelativeTime()
    {
        if (AppliedAt == null)
        {
            RelativeTime = string.Empty;
            _lastDisplayedSeconds = -1;
            return;
        }

        var ts = DateTime.UtcNow - AppliedAt.Value.ToUniversalTime();
        var totalSeconds = (int)Math.Floor(ts.TotalSeconds);
        if (totalSeconds == _lastDisplayedSeconds)
            return;

        _lastDisplayedSeconds = totalSeconds;

        RelativeTime = ts switch
        {
            _ when totalSeconds < 15 => Translations.Common_AppliedJustNow,
            _ when totalSeconds < 60 => string.Format(
                Translations.Common_AppliedSecondsAgo,
                totalSeconds
            ),
            _ when ts.TotalMinutes < 60 => string.Format(
                Translations.Common_AppliedMinutesAgo,
                (int)Math.Floor(ts.TotalMinutes)
            ),
            _ when ts.TotalHours < 24 => string.Format(
                Translations.Common_AppliedHoursAgo,
                (int)Math.Floor(ts.TotalHours)
            ),
            _ when ts.TotalDays < 30 => string.Format(
                Translations.Common_AppliedDaysAgo,
                (int)Math.Floor(ts.TotalDays)
            ),
            _ when ts.TotalDays < 365 => string.Format(
                Translations.Common_AppliedMonthsAgo,
                (int)(ts.TotalDays / 30)
            ),
            _ => string.Format(Translations.Common_AppliedYearsAgo, (int)(ts.TotalDays / 365)),
        };
    }

    partial void OnAppliedAtChanged(DateTime? value)
    {
        _lastDisplayedSeconds = -1;
        UpdateRelativeTime();
    }
}
