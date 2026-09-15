using System.Globalization;

namespace optimizerDuck.Domain.UI;

/// <summary>
///     Represents a selectable language option in the settings UI.
/// </summary>
public record LanguageOption
{
    /// <summary>
    ///     The human-readable name of the language (e.g., "English", "Tiếng Việt").
    /// </summary>
    public required string DisplayName { get; init; }

    public required CultureInfo Culture { get; init; }
}
