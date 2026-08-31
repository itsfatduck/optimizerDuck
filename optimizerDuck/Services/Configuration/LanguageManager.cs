using System.ComponentModel;
using System.Globalization;
using System.Windows;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Configuration;

/// <summary>Language change event data.</summary>
public sealed class LanguageChangedEventArgs(CultureInfo newCulture) : EventArgs
{
    /// <summary>New language that was applied.</summary>
    public CultureInfo NewCulture { get; } = newCulture;
}

/// <summary>App language manager. Singleton that updates UI when language changes.</summary>
public class Loc : INotifyPropertyChanged
{
    /// <summary>Single instance used everywhere (Loc.Instance["Key"]). </summary>
    public static Loc Instance { get; } = new();

    /// <summary>Current UI language.</summary>
    public static CultureInfo CurrentCulture => Translations.Culture;

    /// <summary>True if current language is right-to-left (Arabic, Hebrew).</summary>
    public bool IsRtl => Translations.Culture.TextInfo.IsRightToLeft;

    /// <summary>Layout direction for current language (LeftToRight or RightToLeft).</summary>
    public FlowDirection Direction => IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Get text for key in current language. Returns key if not found.</summary>
    public string this[string key] =>
        Translations.ResourceManager.GetString(key, Translations.Culture) ?? key;

    /// <summary>Always English, even when UI language changes. Use for logs.</summary>
    public static InvariantStrings Invariant { get; } = new();

    /// <summary>English-only text (neutral resources). Not affected by ChangeCulture.</summary>
    public sealed class InvariantStrings
    {
        /// <summary>Get English text for key.</summary>
        public string this[string key] =>
            Translations.ResourceManager.GetString(key, CultureInfo.InvariantCulture) ?? key;

        /// <summary>Get English text with formatting (string.Format).</summary>
        public string this[string key, params object?[] args] => string.Format(this[key], args);
    }

    /// <summary>Get text with formatting, e.g. Loc.Instance["Key", value].</summary>
    public string this[string key, params object?[] args] => string.Format(this[key], args);

    /// <summary>Occurs when language changes. ViewModels refresh cached text here.</summary>
    public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

    /// <summary>Subscribe without keeping object alive. Use for ViewModels that can be closed.</summary>
    public static void AddWeakLanguageChangedHandler(
        EventHandler<LanguageChangedEventArgs> handler
    ) =>
        WeakEventManager<Loc, LanguageChangedEventArgs>.AddHandler(
            Instance,
            nameof(LanguageChanged),
            handler
        );

    /// <summary>Change UI language and refresh all text and layout.</summary>
    public void ChangeCulture(CultureInfo culture)
    {
        Translations.Culture = culture;
        OnPropertyChanged(nameof(IsRtl));
        OnPropertyChanged(nameof(Direction));
        OnPropertyChanged("Item[]");
        LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(culture));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
