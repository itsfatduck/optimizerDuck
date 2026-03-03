using System.Globalization;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Managers;

/// <summary>
    ///     Provides localization and culture management for the application.
    /// </summary>
public class Loc
{
    /// <summary>
    ///     Gets the singleton instance of the localization manager.
    /// </summary>
    public static Loc Instance { get; } = new();

    /// <summary>
    ///     Gets the current culture information.
    /// </summary>
    public static CultureInfo CurrentCulture => Translations.Culture;

    /// <summary>
    ///     Gets the localized string for the specified key.
    /// </summary>
    /// <param name="key">The resource key to look up.</param>
    /// <returns>The localized string or the key itself if not found.</returns>
    public string this[string key] =>
        Translations.ResourceManager.GetString(key, Translations.Culture) ?? key;

    /// <summary>
    ///     Changes the current culture for localization.
    /// </summary>
    /// <param name="culture">The new culture to apply.</param>
    public void ChangeCulture(CultureInfo culture)
    {
        Translations.Culture = culture;
    }
}