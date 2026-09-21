using System.Globalization;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Optimizations.Models.ScheduledTask;

/// <summary>
///     Display data for one scheduled-task trigger: a localized label plus a detail
///     built from the trigger's own data. Falls back to raw library text only when no
///     localization fits, and appends the repetition schedule when present.
/// </summary>
public sealed record ScheduledTaskTriggerInfo(
    string LabelKey,
    string? DetailKey,
    string[] Args,
    string? Raw,
    string? RepeatKey = null,
    string[]? RepeatArgs = null
)
{
    public string Label => Loc.Instance[LabelKey];

    public string Detail
    {
        get
        {
            var text = DetailKey is null
                ? Raw ?? Label
                : string.Format(CultureInfo.CurrentCulture, Loc.Instance[DetailKey], [.. Args]);
            return RepeatKey is null
                ? text
                : text
                    + string.Format(
                        CultureInfo.CurrentCulture,
                        Loc.Instance[RepeatKey],
                        [.. (RepeatArgs ?? [])]
                    );
        }
    }
}

/// <summary>
///     One trigger badge: the localized label on the chip plus the detail the tooltip shows.
///     The list is rebuilt on a language change, so both strings follow the culture.
/// </summary>
public sealed record ScheduledTaskTriggerBadge(string Label, string Detail);
