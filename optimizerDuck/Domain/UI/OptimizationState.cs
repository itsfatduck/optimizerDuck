using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;

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

    static OptimizationState()
    {
        // Static handler held by the Loc singleton: references no instance, so the
        // WeakReference tracking above still allows instances to be collected.
        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    private static void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        lock (_lock)
        {
            for (var i = _instances.Count - 1; i >= 0; i--)
            {
                if (!_instances[i].TryGetTarget(out var instance))
                {
                    _instances.RemoveAt(i);
                    continue;
                }

                instance.ForceUpdateRelativeTime();
            }
        }
    }

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

        // Defer timer creation to avoid accessing dispatcher during static init.
        // NOTE: This type lives in Domain/UI but owns a DispatcherTimer (WPF). Ideally
        // move to optimizerDuck.UI or Common — kept here to avoid churn. We route
        // through the dispatcher indirection rather than hard-referencing UI assemblies
        // from pure domain logic.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return; // headless/tests — no timer needed
        _ = dispatcher.InvokeAsync(() =>
        {
            lock (_lock)
            {
                if (_globalTimer != null)
                    return;
                _globalTimer = new DispatcherTimer(
                    TimeSpan.FromSeconds(1),
                    DispatcherPriority.Render,
                    (s, e) => UpdateAllRelativeTimes(),
                    dispatcher
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
        RelativeTime = ComputeRelativeTime();
    }

    private void ForceUpdateRelativeTime()
    {
        _lastDisplayedSeconds = -1;
        UpdateRelativeTime();
    }

    private string ComputeRelativeTime()
    {
        if (AppliedAt == null)
            return string.Empty;

        var ts = DateTime.UtcNow - AppliedAt.Value.ToUniversalTime();
        var totalSeconds = (int)Math.Floor(ts.TotalSeconds);
        if (totalSeconds == _lastDisplayedSeconds)
            return RelativeTime ?? string.Empty;

        _lastDisplayedSeconds = totalSeconds;

        return ts switch
        {
            _ when totalSeconds < 15 => Loc.Instance["Common.AppliedJustNow"],
            _ when totalSeconds < 60 => Loc.Instance["Common.AppliedSecondsAgo", totalSeconds],
            _ when ts.TotalMinutes < 60 => Loc.Instance[
                "Common.AppliedMinutesAgo",
                (int)Math.Floor(ts.TotalMinutes)
            ],
            _ when ts.TotalHours < 24 => Loc.Instance[
                "Common.AppliedHoursAgo",
                (int)Math.Floor(ts.TotalHours)
            ],
            _ when ts.TotalDays < 30 => Loc.Instance[
                "Common.AppliedDaysAgo",
                (int)Math.Floor(ts.TotalDays)
            ],
            _ when ts.TotalDays < 365 => Loc.Instance[
                "Common.AppliedMonthsAgo",
                (int)(ts.TotalDays / 30)
            ],
            _ => Loc.Instance["Common.AppliedYearsAgo", (int)(ts.TotalDays / 365)],
        };
    }

    partial void OnAppliedAtChanged(DateTime? value)
    {
        _lastDisplayedSeconds = -1;
        UpdateRelativeTime();
    }
}
