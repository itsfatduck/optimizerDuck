using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Common.Extensions;

/// <summary>
///     Culture-invariant logging helpers for <see cref="IOptimization" /> and
///     <see cref="ICustomizeSetting" />. Real models derive from <see cref="BaseOptimization" />
///     / <see cref="BaseCustomizeSetting" />, which provide the invariant properties; these
///     extensions forward to them for interface-typed call sites and fall back to the
///     localized names for any other implementation.
/// </summary>
public static class LocalizationLogExtensions
{
    /// <summary>Gets the culture-invariant (neutral English) name for logging.</summary>
    public static string LogName(this IOptimization optimization) =>
        optimization is BaseOptimization baseOptimization
            ? baseOptimization.LogName
            : optimization.Name;

    /// <summary>Gets the culture-invariant (neutral English) description for logging.</summary>
    public static string LogShortDescription(this IOptimization optimization) =>
        optimization is BaseOptimization baseOptimization
            ? baseOptimization.LogShortDescription
            : optimization.ShortDescription;

    /// <summary>Gets the culture-invariant (neutral English) name for logging.</summary>
    public static string LogName(this ICustomizeSetting setting) =>
        setting is BaseCustomizeSetting baseSetting ? baseSetting.LogName : setting.Name;

    /// <summary>Gets the culture-invariant (neutral English) description for logging.</summary>
    public static string LogDescription(this ICustomizeSetting setting) =>
        setting is BaseCustomizeSetting baseSetting
            ? baseSetting.LogDescription
            : setting.Description;
}
