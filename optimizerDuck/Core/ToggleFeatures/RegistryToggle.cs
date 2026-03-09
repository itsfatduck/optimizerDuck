using Microsoft.Win32;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Core.ToggleFeatures;

public class RegistryToggle
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public object OnValue { get; init; } = 1;
    public object OffValue { get; init; } = 0;
    public object DefaultValue { get; init; } = 0;
    public bool CheckKeyExists { get; init; } = false;
    public RegistryValueKind ValueKind { get; init; } = RegistryValueKind.DWord;

    public bool GetState()
    {
        var rawValue = GetRawValue();
        return CompareValues(rawValue);
    }

    private object? GetRawValue()
    {
        try
        {
            var item = new RegistryItem(Path, Name);
            var value = RegistryService.Read<object?>(item);

            if (value == null)
                return CheckKeyExists ? null : DefaultValue;

            return value;
        }
        catch
        {
            return CheckKeyExists ? null : DefaultValue;
        }
    }

    private bool CompareValues(object? value)
    {
        if (value == null && OnValue == null)
            return true;
        if (value == null || OnValue == null)
            return false;

        if (value is int intVal && OnValue is int onInt)
            return intVal == onInt;

        if (value is string strVal)
            return strVal.Equals(OnValue.ToString(), StringComparison.OrdinalIgnoreCase);

        return value.Equals(OnValue);
    }

    public void SetState(bool isOn)
    {
        var item = new RegistryItem(Path, Name, isOn ? OnValue : OffValue, ValueKind);
        RegistryService.Write(item);
    }
}
