using Microsoft.Win32;
using optimizerDuck.Domain.Optimizations.Models.Services;

namespace optimizerDuck.Domain.Customize.Models;

/// <summary>
///     Binds a <see cref="SettingOption" /> to one or more registry values.
///     Used by <see cref="BaseCustomizeSetting" /> to auto-read/write registry for Dropdown settings.
/// </summary>
public record RegistryBinding(
    string Path,
    string? Name,
    object? Value,
    RegistryValueKind ValueKind = RegistryValueKind.DWord
)
{
    public RegistryItem ToRegistryItem() => new(Path, Name, Value, ValueKind);
}
