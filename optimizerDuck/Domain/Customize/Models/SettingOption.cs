namespace optimizerDuck.Domain.Customize.Models;

public sealed record SettingOption(
    string DisplayName,
    object Value,
    IReadOnlyList<RegistryBinding>? Bindings = null
)
{
    /// <summary>
    ///     Primary binding — the first binding in the list, or null.
    ///     Used by base class for single-key auto-read/write.
    /// </summary>
    public RegistryBinding? PrimaryBinding => Bindings is { Count: > 0 } ? Bindings[0] : null;
}
