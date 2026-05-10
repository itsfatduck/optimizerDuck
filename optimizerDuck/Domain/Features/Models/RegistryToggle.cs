using Microsoft.Win32;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Domain.Features.Models;

public class RegistryToggle
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public object? OnValue { get; init; } = 1;
    public object? OffValue { get; init; } = 0;
    public object? DefaultValue { get; init; } = 0;
    public bool TreatMissingAsDefault { get; init; } = false;
    public RegistryValueKind ValueKind { get; init; } = RegistryValueKind.DWord;

    public bool GetState()
    {
        var value = GetRawValue();

        if (value == null)
        {
            // If value is null and TreatMissingAsDefault is false,
            // treat it as the default state (off)
            return TreatMissingAsDefault ? AreEqual(DefaultValue, OnValue) : false;
        }

        return AreEqual(value, OnValue);
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

    private object? GetRawValue()
    {
        var value = RegistryService.Read<object?>(new RegistryItem(Path, Name));

        if (value == null && TreatMissingAsDefault)
            return DefaultValue;

        return value;
    }

    public void SetState(bool isOn)
    {
        var targetValue = isOn ? OnValue : OffValue;
        if (targetValue == null)
            RegistryService.DeleteValue(new RegistryItem(Path, Name));
        else
            RegistryService.Write(new RegistryItem(Path, Name, targetValue, ValueKind));
    }
}
