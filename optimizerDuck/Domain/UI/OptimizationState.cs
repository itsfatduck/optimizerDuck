using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.UI;

/// <summary>
///     Tracks the applied state and timing of an optimization.
/// </summary>
public partial class OptimizationState : ObservableObject
{
    private static DispatcherTimer? _globalTimer;
    private static readonly List<WeakReference<OptimizationState>> _instances = [];
    private static readonly Lock _lock = new();

    private int _lastBucket = -1;

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

    [ObservableProperty]
    private DateTime? appliedAt;

    [ObservableProperty]
    private bool isApplied;

    /// <summary>
    ///     Indicates that the last apply reported nothing to do because the machine already
    ///     matched the optimization.
    /// </summary>
    [ObservableProperty]
    private bool isAlreadyOptimal;

    /// <summary>The relative time text, for example "5 minutes ago".</summary>
    [ObservableProperty]
    private string? relativeTime = string.Empty;

    /// <summary>
    ///     One line describing what the last recorded apply did, read from the record when there
    ///     is one. Empty when nothing is known.
    /// </summary>
    [ObservableProperty]
    private string appliedSummary = string.Empty;

    /// <summary>The applied badge tooltip: what the run did, and what a click does.</summary>
    public string AppliedTooltip =>
        string.IsNullOrEmpty(AppliedSummary)
            ? Loc.Instance["Optimizer.UI.State.Applied.Tooltip.NoSummary"]
            : Loc.Instance["Optimizer.UI.State.Applied.Tooltip", AppliedSummary];

    partial void OnAppliedSummaryChanged(string value) => OnPropertyChanged(nameof(AppliedTooltip));

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

        // Defer timer creation to avoid accessing the dispatcher during static init. The
        // dispatcher is reached through Application.Current so Domain keeps no hard reference
        // to the UI.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return; // headless/tests, no timer needed
        _ = dispatcher.InvokeAsync(() =>
        {
            lock (_lock)
            {
                if (_globalTimer != null)
                    return;
                _globalTimer = new DispatcherTimer(
                    TimeSpan.FromSeconds(1),
                    DispatcherPriority.Background,
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
                {
                    if (instance.AppliedAt == null)
                    {
                        if (!string.IsNullOrEmpty(instance.RelativeTime))
                            instance.UpdateRelativeTime();
                    }
                    else
                    {
                        instance.UpdateRelativeTime();
                    }
                }
                else
                    _instances.RemoveAt(i);

            if (_instances.Count == 0 && _globalTimer != null)
            {
                _globalTimer.Stop();
                _globalTimer = null;
            }
        }
    }

    private void UpdateRelativeTime()
    {
        var newTime = ComputeRelativeTime();
        if (RelativeTime != newTime)
            RelativeTime = newTime;
    }

    private void ForceUpdateRelativeTime()
    {
        _lastBucket = -1;
        UpdateRelativeTime();
    }

    private string ComputeRelativeTime()
    {
        if (AppliedAt == null)
            return string.Empty;

        var ts = DateTime.UtcNow - AppliedAt.Value.ToUniversalTime();
        var totalSeconds = (int)Math.Floor(ts.TotalSeconds);
        if (totalSeconds < 0)
            totalSeconds = 0;

        int bucket;
        if (totalSeconds < 15)
            bucket = 0;
        else if (totalSeconds < 60)
            bucket = totalSeconds;
        else if (ts.TotalMinutes < 60)
            bucket = 1000 + (int)Math.Floor(ts.TotalMinutes);
        else if (ts.TotalHours < 24)
            bucket = 10000 + (int)Math.Floor(ts.TotalHours);
        else if (ts.TotalDays < 30)
            bucket = 100000 + (int)Math.Floor(ts.TotalDays);
        else if (ts.TotalDays < 365)
            bucket = 1000000 + (int)(ts.TotalDays / 30);
        else
            bucket = 10000000 + (int)(ts.TotalDays / 365);

        if (bucket == _lastBucket)
            return RelativeTime ?? string.Empty;

        _lastBucket = bucket;

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
        _lastBucket = -1;
        UpdateRelativeTime();
    }
}
