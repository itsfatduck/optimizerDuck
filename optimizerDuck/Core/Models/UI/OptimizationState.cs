using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Core.Models.UI;

public partial class OptimizationState : ObservableObject
{
    private readonly DispatcherTimer _timer;
    private int _lastDisplayedSeconds = -1;

    [ObservableProperty] private DateTime? appliedAt;
    [ObservableProperty] private bool isApplied;
    [ObservableProperty] private string? relativeTime = string.Empty;

    [ObservableProperty] private OptimizationRisk risk;

    public OptimizationState()
    {
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Render,
            (s, e) => UpdateRelativeTime(),
            Application.Current?.Dispatcher
            ?? Dispatcher.CurrentDispatcher
        );
        _timer.Start();
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
        if (totalSeconds == _lastDisplayedSeconds) return;

        _lastDisplayedSeconds = totalSeconds;

        RelativeTime = ts switch
        {
            _ when totalSeconds < 15 => Translations.Common_AppliedJustNow,
            _ when totalSeconds < 60 =>
                string.Format(Translations.Common_AppliedSecondsAgo, totalSeconds),
            _ when ts.TotalMinutes < 60 =>
                string.Format(Translations.Common_AppliedMinutesAgo, (int)Math.Floor(ts.TotalMinutes)),
            _ when ts.TotalHours < 24 =>
                string.Format(Translations.Common_AppliedHoursAgo, (int)Math.Floor(ts.TotalHours)),
            _ when ts.TotalDays < 30 =>
                string.Format(Translations.Common_AppliedDaysAgo, (int)Math.Floor(ts.TotalDays)),
            _ when ts.TotalDays < 365 =>
                string.Format(Translations.Common_AppliedMonthsAgo, (int)(ts.TotalDays / 30)),
            _ => string.Format(Translations.Common_AppliedYearsAgo, (int)(ts.TotalDays / 365))
        };
    }

    partial void OnAppliedAtChanged(DateTime? value)
    {
        _lastDisplayedSeconds = -1;
        UpdateRelativeTime();
    }
}