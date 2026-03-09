using Microsoft.Win32;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Core.Models.Features;

public class RegistryToggle
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required object? OnValue { get; init; } = 1;
    public required object? OffValue { get; init; } = 0;
    public required object? DefaultValue { get; init; } = 0;
    public bool TreatMissingAsDefault { get; init; } = false;
    public RegistryValueKind ValueKind { get; init; } = RegistryValueKind.DWord;


    public bool GetState()
    {
        var value = GetRawValue();
        if (value == null)
            return OnValue == null;

        return AreEqual(value, OnValue);
    }

    private static bool AreEqual(object? a, object? b)
    {
        if (a == null && b == null)
            return true;

        if (a == null || b == null)
            return false;

        if (a is IConvertible && b is IConvertible)
        {
            var da = Convert.ToDecimal(a);
            var db = Convert.ToDecimal(b);
            return da == db;
        }

        return a.ToString()?.Equals(
            b.ToString(),
            StringComparison.OrdinalIgnoreCase
        ) == true;
    }

    private object? GetRawValue()
    {
        var value = RegistryService.Read<object?>(new RegistryItem(Path, Name));
        return value ?? (TreatMissingAsDefault ? null : DefaultValue);
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