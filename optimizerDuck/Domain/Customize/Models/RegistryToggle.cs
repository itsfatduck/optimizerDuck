using Microsoft.Win32;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.Optimization.Providers;

namespace optimizerDuck.Domain.Customize.Models;

/// <summary>
///     Represents a single registry key-value pair that can be toggled on or off.
///     <see cref="OnValues" /> and <see cref="OffValues" /> support multiple values
///     (e.g. <c>null</c> means "key absent").
/// </summary>
public class RegistryToggle
{
    /// <summary>Gets the full registry key path (e.g., <c>HKCU\Software\MyApp</c>).</summary>
    public required string Path { get; init; }

    /// <summary>Gets the name of the registry value.</summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets the list of values that represent the "on" state.
    ///     <c>null</c> in the list means "key absent = on".
    ///     Default is <c>[1]</c>.
    /// </summary>
    public IReadOnlyList<object?> OnValues { get; init; } = [1];

    /// <summary>
    ///     Gets the list of values that represent the "off" state.
    ///     <c>null</c> in the list means "key absent = off".
    ///     Default is <c>[0]</c>.
    /// </summary>
    public IReadOnlyList<object?> OffValues { get; init; } = [0];

    /// <summary>
    ///     Gets the default state value when the key is missing. Used for Reset to Default.
    ///     Default is <c>0</c>.
    /// </summary>
    public object? DefaultValue { get; init; } = 0;

    /// <summary>
    ///     Gets a value that indicates whether this toggle is optional (non-required for state detection).
    ///     Default is <see langword="false"/>.
    /// </summary>
    public bool IsOptional { get; init; } = false;

    /// <summary>Gets the registry value type. Default is <see cref="RegistryValueKind.DWord"/>.</summary>
    public RegistryValueKind ValueKind { get; init; } = RegistryValueKind.DWord;

    /// <summary>
    ///     Reads the registry and returns whether the toggle is currently on.
    ///     Returns <see langword="true" /> if the registry value matches any value in <see cref="OnValues" />.
    /// </summary>
    public bool GetState()
    {
        var value = GetRawValue();
        return IsValueInList(value, OnValues);
    }

    public void SetState(bool isOn)
    {
        var targetValues = isOn ? OnValues : OffValues;

        // First value in the list is the primary value to write
        if (targetValues is { Count: > 0 } && targetValues[0] is { } firstValue)
            RegistryService.Write(new RegistryItem(Path, Name, firstValue, ValueKind));
        else
            RegistryService.DeleteValue(new RegistryItem(Path, Name));
    }

    private object? GetRawValue()
    {
        return RegistryService.Read<object?>(new RegistryItem(Path, Name));
    }

    private static bool IsValueInList(object? value, IReadOnlyList<object?> values)
    {
        foreach (var target in values)
        {
            if (AreEqual(value, target))
                return true;
        }

        return false;
    }

    private static bool AreEqual(object? a, object? b)
    {
        if (a == null && b == null)
            return true;

        if (a == null || b == null)
            return false;

        // Handle numeric types with direct comparison to avoid precision loss
        if (a is IConvertible && b is IConvertible)
        {
            try
            {
                // Try direct type comparison first
                if (a.GetType() == b.GetType())
                {
                    return a.Equals(b);
                }

                // For numeric types, compare as the most precise type
                var typeA = a.GetType();
                var typeB = b.GetType();

                // If both are integers, compare as long
                if (
                    (
                        typeA == typeof(int)
                        || typeA == typeof(long)
                        || typeA == typeof(short)
                        || typeA == typeof(byte)
                    )
                    && (
                        typeB == typeof(int)
                        || typeB == typeof(long)
                        || typeB == typeof(short)
                        || typeB == typeof(byte)
                    )
                )
                {
                    return Convert.ToInt64(a) == Convert.ToInt64(b);
                }

                // For floating point, compare as double
                if (
                    (typeA == typeof(float) || typeA == typeof(double) || typeA == typeof(decimal))
                    && (
                        typeB == typeof(float)
                        || typeB == typeof(double)
                        || typeB == typeof(decimal)
                    )
                )
                {
                    return Convert.ToDouble(a) == Convert.ToDouble(b);
                }

                // Fallback to decimal comparison for other convertible types
                var da = Convert.ToDecimal(a);
                var db = Convert.ToDecimal(b);
                return da == db;
            }
            catch
            {
                // If conversion fails, fall back to string comparison
            }
        }

        // String comparison - use ordinal for case-sensitive, ordinal ignore case for case-insensitive
        var strA = a.ToString();
        var strB = b.ToString();
        return strA != null && strB != null && strA.Equals(strB, StringComparison.Ordinal);
    }
}
