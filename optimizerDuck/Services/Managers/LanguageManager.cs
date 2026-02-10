using System.Globalization;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Managers;

public class Loc
{
    public static Loc Instance { get; } = new();

    public static CultureInfo CurrentCulture => Translations.Culture;

    public string this[string key] =>
        Translations.ResourceManager.GetString(key, Translations.Culture) ?? key;

    public void ChangeCulture(CultureInfo culture)
    {
        Translations.Culture = culture;
    }
}