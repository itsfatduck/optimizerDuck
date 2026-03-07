using Microsoft.Win32;

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

    private static readonly Dictionary<string, object?> _cache = new();
    private static readonly object _lock = new();

    public bool GetState()
    {
        lock (_lock)
        {
            var cacheKey = $"{Path}\\{Name}";
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return CompareValues(cached);
            }

            var state = GetStateInternal();
            _cache[cacheKey] = GetRawValue();
            return state;
        }
    }

    private object? GetRawValue()
    {
        try
        {
            using var key = GetRegistryKey(Path, false);
            if (key == null)
                return CheckKeyExists ? null : DefaultValue;

            var value = key.GetValue(Name, DefaultValue);
            return value;
        }
        catch
        {
            return CheckKeyExists ? null : DefaultValue;
        }
    }

    private bool GetStateInternal()
    {
        try
        {
            using var key = GetRegistryKey(Path, false);
            if (key == null)
            {
                return CompareValues(CheckKeyExists ? null : DefaultValue);
            }

            var value = key.GetValue(Name, DefaultValue);
            return CompareValues(value);
        }
        catch
        {
            return CompareValues(DefaultValue);
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
        lock (_lock)
        {
            var cacheKey = $"{Path}\\{Name}";
            _cache.Remove(cacheKey);

            try
            {
                using var key = GetRegistryKey(Path, true);
                if (key == null)
                    return;

                var value = isOn ? OnValue : OffValue;
                key.SetValue(Name, value, ValueKind);
                _cache[cacheKey] = value;
            }
            catch
            {
            }
        }
    }

    public void ClearCache()
    {
        lock (_lock)
        {
            var cacheKey = $"{Path}\\{Name}";
            _cache.Remove(cacheKey);
        }
    }

    public static void ClearAllCache()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }

    private static RegistryKey? GetRegistryKey(string path, bool writable)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var rootKeyName = path.Split('\\')[0].Replace("HKEY_CURRENT_USER", "HKCU")
            .Replace("HKEY_LOCAL_MACHINE", "HKLM")
            .Replace("HKEY_CLASSES_ROOT", "HKCR")
            .Replace("HKEY_USERS", "HKU")
            .Replace("HKEY_CURRENT_CONFIG", "HKCC");

        var subPath = string.Join("\\", path.Split('\\').Skip(1));

        var rootKey = rootKeyName switch
        {
            "HKCU" => Registry.CurrentUser,
            "HKLM" => Registry.LocalMachine,
            "HKCR" => Registry.ClassesRoot,
            "HKU" => Registry.Users,
            "HKCC" => Registry.CurrentConfig,
            _ => null
        };

        if (rootKey == null)
            return null;

        return writable
            ? rootKey.CreateSubKey(subPath, true)
            : rootKey.OpenSubKey(subPath, false);
    }
}
