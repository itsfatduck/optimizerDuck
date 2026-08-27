using Microsoft.Win32;
using optimizerDuck.Domain.Optimizations.Models.Services;

namespace optimizerDuck.Domain.Customize.Models;

/// <summary>
///     Binds a <see cref="SettingOption" /> to one or more registry values.
///     Used by <see cref="BaseCustomizeSetting" /> to auto-read/write registry for Dropdown settings.
///     Supports matching multiple accepted values (e.g. 1 or null for default values).
/// </summary>
public record RegistryBinding(
    string Path,
    string? Name,
    object? Value,
    RegistryValueKind ValueKind = RegistryValueKind.DWord,
    IReadOnlyList<object?>? MatchValues = null
)
{
    /// <summary>
    ///     Checks whether an actual value read from the registry matches this binding.
    ///     When <see cref="MatchValues"/> is declared, any value in the list matches.
    ///     Otherwise, the actual value must equal <see cref="Value"/>.
    /// </summary>
    public bool Matches(object? actual)
    {
        if (MatchValues is { Count: > 0 })
        {
            return MatchValues.Any(v => BaseCustomizeSetting.ValuesEqual(actual, v));
        }

        return BaseCustomizeSetting.ValuesEqual(actual, Value);
    }

    public RegistryItem ToRegistryItem() => new(Path, Name, Value, ValueKind);
}
