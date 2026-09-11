using System.Globalization;
using System.Runtime.CompilerServices;
using optimizerDuck.Resources.Languages;
using Xunit;

// The suite exercises the real registry, the real filesystem, real child processes, and
// process-wide statics (notably Loc.Instance's culture and ReflectionHelper's cache).
// Running test classes in parallel makes those shared statics race: a culture change in
// one class flips the localized text another class is asserting on. Serialize instead.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

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
