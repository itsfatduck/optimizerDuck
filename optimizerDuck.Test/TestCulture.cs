using System.Globalization;
using System.Runtime.CompilerServices;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Test;

internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Translations.Culture = culture;
    }
}
