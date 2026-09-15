using Microsoft.Win32;

namespace optimizerDuck.Domain.Optimizations.Models.Services;

/// <summary>
///     Compares registry values using the semantics of their <see cref="RegistryValueKind" />.
/// </summary>
public static class RegistryValues
{
    public static bool Equal(object? actual, object? expected, RegistryValueKind kind)
    {
        if (actual == null || expected == null)
            return actual == null && expected == null;
        return kind switch
        {
            RegistryValueKind.DWord => Convert.ToInt32(actual) == Convert.ToInt32(expected),
            RegistryValueKind.QWord => Convert.ToInt64(actual) == Convert.ToInt64(expected),
            RegistryValueKind.String or RegistryValueKind.ExpandString => string.Equals(
                actual.ToString(),
                expected.ToString(),
                StringComparison.Ordinal
            ),
            RegistryValueKind.MultiString when actual is string[] a && expected is string[] e =>
                a.SequenceEqual(e),
            RegistryValueKind.Binary when actual is byte[] ab && expected is byte[] eb =>
                ab.SequenceEqual(eb),
            _ => Equals(actual, expected),
        };
    }
}
