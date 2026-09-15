using System.Globalization;
using System.Runtime.CompilerServices;
using optimizerDuck.Resources.Languages;
using Xunit;

// The suite touches the real registry, filesystem and child processes, and shares
// process-wide statics (Loc.Instance's culture, ReflectionHelper's cache). Parallel
// test classes race on those, so the assembly runs serialized.
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
