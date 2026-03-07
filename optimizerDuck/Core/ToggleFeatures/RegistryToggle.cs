using Microsoft.Win32;

namespace optimizerDuck.Core.ToggleFeatures;

public class RegistryToggle
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int OnValue { get; init; }
    public int OffValue { get; init; }
    public int DefaultValue { get; init; }

    public bool GetState()
    {
        try
        {
            var value = Registry.GetValue(Path, Name, DefaultValue);
            if (value == null)
                return false;
            return (int)value == OnValue;
        }
        catch
        {
            return false;
        }
    }

    public void SetState(bool isOn)
    {
        try
        {
            Registry.SetValue(Path, Name, isOn ? OnValue : OffValue, RegistryValueKind.DWord);
        }
        catch
        {
        }
    }
}
