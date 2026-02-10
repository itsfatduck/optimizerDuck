using System.Globalization;

namespace optimizerDuck.Core.Models.UI;

public record LanguageOption(
    string DisplayName,
    CultureInfo Culture
);