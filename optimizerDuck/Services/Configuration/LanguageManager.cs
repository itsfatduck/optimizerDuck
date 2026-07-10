using System.ComponentModel;
using System.Globalization;
using System.Windows;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Configuration;

/// <summary>
///     Provides localization and culture management for the application.
///     Implements <see cref="INotifyPropertyChanged" /> so UI bindings (e.g., FlowDirection) update on culture change.
/// </summary>
public class Loc : INotifyPropertyChanged
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
    ///     Gets whether the current culture uses a right-to-left writing system.
    /// </summary>
    public bool IsRtl => Translations.Culture.TextInfo.IsRightToLeft;

    /// <summary>
    ///     Gets the <see cref="FlowDirection" /> corresponding to the current culture.
    ///     Returns <see cref="FlowDirection.RightToLeft" /> for RTL languages, otherwise <see cref="FlowDirection.LeftToRight" />.
    /// </summary>
    public FlowDirection Direction => IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

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
        OnPropertyChanged(nameof(IsRtl));
        OnPropertyChanged(nameof(Direction));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Raises the <see cref="PropertyChanged" /> event.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
