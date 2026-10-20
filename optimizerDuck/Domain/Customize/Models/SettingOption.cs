namespace optimizerDuck.Domain.Customize.Models;

/// <summary>
///     Represents a selectable option for a dropdown customization setting.
/// </summary>
/// <param name="DisplayName">The localized label displayed in the UI.</param>
/// <param name="Value">The value identifier for this option.</param>
/// <param name="Bindings">The registry bindings written when this option is selected.</param>
public sealed record SettingOption(
    string DisplayName,
    object Value,
    IReadOnlyList<RegistryBinding>? Bindings = null
)
{
    /// <summary>
    ///     Gets the primary registry binding used for single-key auto-read and write operations, or <see langword="null" />.
    /// </summary>
    public RegistryBinding? PrimaryBinding => Bindings is { Count: > 0 } ? Bindings[0] : null;
}
