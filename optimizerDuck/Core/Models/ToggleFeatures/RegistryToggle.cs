using Microsoft.Win32;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Core.Models.ToggleFeatures;

public class RegistryToggle
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public object? OnValue { get; init; } = 1;
    public object? OffValue { get; init; } = 0;
    public object? DefaultValue { get; init; } = 0;
    public bool CheckKeyExists { get; init; } = false;
    public RegistryValueKind ValueKind { get; init; } = RegistryValueKind.DWord;

    public bool GetState()
    {
        var value = GetRawValue();
        if (value == null)
            return OnValue == null;

        // Unified comparison using string representation for cross-type compatibility (e.g. int vs long vs string)
        var currentStr = value.ToString() ?? string.Empty;
        var onStr = OnValue?.ToString() ?? string.Empty;
        
        return currentStr.Equals(onStr, StringComparison.OrdinalIgnoreCase);
    }

    private object? GetRawValue()
    {
        var value = RegistryService.Read<object?>(new RegistryItem(Path, Name));
        return value ?? (CheckKeyExists ? null : DefaultValue);
    }

    public void SetState(bool isOn)
    {
        var targetValue = isOn ? OnValue : OffValue;
        if (targetValue != null)
        {
            RegistryService.Write(new RegistryItem(Path, Name, targetValue, ValueKind));
        }
    }
}
