using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Models;
using optimizerDuck.UI.Logger;
using System.Diagnostics.CodeAnalysis;

namespace optimizerDuck.Core.Services;

public static class RegistryService
{
    private static readonly Dictionary<string, RegistryKey> RootKeysMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["HKLM"] = Registry.LocalMachine,
            ["HKLM:"] = Registry.LocalMachine,
            ["HKEY_LOCAL_MACHINE"] = Registry.LocalMachine,

            ["HKCU"] = Registry.CurrentUser,
            ["HKCU:"] = Registry.CurrentUser,
            ["HKEY_CURRENT_USER"] = Registry.CurrentUser,

            ["HKCR"] = Registry.ClassesRoot,
            ["HKCR:"] = Registry.ClassesRoot,
            ["HKEY_CLASSES_ROOT"] = Registry.ClassesRoot,

            ["HKU"] = Registry.Users,
            ["HKU:"] = Registry.Users,
            ["HKEY_USERS"] = Registry.Users,

            ["HKCC"] = Registry.CurrentConfig,
            ["HKCC:"] = Registry.CurrentConfig,
            ["HKEY_CURRENT_CONFIG"] = Registry.CurrentConfig
        };

    private static bool TryParsePath(
        string fullPath,
        [NotNullWhen(true)] out RegistryKey? rootKey,
        [NotNullWhen(true)] out string? subPath)
    {
        rootKey = null;
        subPath = null;

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            ServiceTracker.Current?.Log.LogError("Registry path is null or empty");
            ServiceTracker.Current?.Track(nameof(RegistryService), false);
            return false;
        }

        var parts = fullPath.AsSpan();
        var idx = parts.IndexOf('\\');
        var rootToken = idx > 0 ? parts[..idx].ToString() : parts.ToString();

        if (!RootKeysMap.TryGetValue(rootToken, out rootKey))
        {
            ServiceTracker.Current?.Log.LogError("Unknown root key: {RootToken}", rootToken);
            ServiceTracker.Current?.Track(nameof(RegistryService), false);
            return false;
        }

        subPath = idx > 0 ? parts[(idx + 1)..].ToString() : string.Empty;
        return true;
    }

    private static bool TryOpenSubKey(
        RegistryKey rootKey,
        string? subPath,
        [NotNullWhen(true)] out RegistryKey? key,
        bool writable,
        bool createIfMissing,
        out bool shouldDispose)
    {
        key = null;
        shouldDispose = false;

        if (string.IsNullOrEmpty(subPath))
        {
            key = rootKey;
            return true;
        }

        try
        {
            var opened = writable
                ? rootKey.OpenSubKey(subPath, true)
                : rootKey.OpenSubKey(subPath);

            if (opened == null && writable && createIfMissing)
                opened = rootKey.CreateSubKey(subPath, true);

            if (opened != null)
            {
                key = opened;
                shouldDispose = true;
                return true;
            }

            ServiceTracker.Current?.Log.LogError("SubKey not found: {Root}\\{SubPath}", rootKey.Name, subPath);
            ServiceTracker.Current?.Track(nameof(RegistryService), false);
            return false;
        }
        catch (Exception ex)
        {
            ServiceTracker.Current?.Log.LogError(ex, "Failed to {Operation} sub key: {RootKey}\\{SubPath}",
                writable ? "create/open" : "open", rootKey.Name, subPath);
            ServiceTracker.Current?.Track(nameof(RegistryService), false);
            return false;
        }
    }

    private static T? WithKey<T>(RegistryItem item, Func<RegistryKey, T?> action, bool writable = false,
        bool createIfMissing = false)
    {
        if (!TryParsePath(item.Path, out var rootKey, out var subPath))
            return default;

        if (!TryOpenSubKey(rootKey, subPath, out var subKey, writable, createIfMissing, out var shouldDispose))
            return default;

        try
        {
            return action(subKey);
        }
        finally
        {
            if (shouldDispose)
                subKey.Dispose();
        }
    }

    public static T? Read<T>(RegistryItem item)
    {
        return WithKey(item, key =>
        {
            try
            {
                var value = key.GetValue(item.Name);
                if (value is null) return default;
                if (value is T tValue) return tValue;

                if (typeof(T) == typeof(byte[]) && value is byte[] bytes) return (T)(object)bytes;
                if (typeof(T) == typeof(string[]) && value is string[] arr) return (T)(object)arr;

                if (typeof(T) == typeof(int) && value is int i) return (T)(object)i;
                if (typeof(T) == typeof(bool) && value is int i2) return (T)(object)(i2 != 0);

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                ServiceTracker.Current?.Log.LogError(ex, "Failed to read or convert registry value {Path}:{Name}",
                    item.Path, item.Name);
                return default;
            }
        });
    }

    public static bool Write(RegistryItem registryItem)
    {
        if (registryItem.Value == null)
        {
            ServiceTracker.Current?.Log.LogError("Value can't be null when writing! {Path}:{Name}", registryItem.Path,
                registryItem.Name);
            ServiceTracker.Current?.Track(nameof(Write), false);
            return false;
        }

        return WithKey(registryItem, key =>
        {
            try
            {
                key.SetValue(registryItem.Name, registryItem.Value, registryItem.Kind);
                ServiceTracker.Current?.Track(nameof(Write), true);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                ServiceTracker.Current?.Log.LogError(
                    "Failed to write registry value {Path}:{Name} due to missing access",
                    registryItem.Path, registryItem.Name);
                ServiceTracker.Current?.Track(nameof(Write), false);
                return false;
            }
            catch (Exception ex)
            {
                ServiceTracker.Current?.Log.LogError(ex, "Failed to write registry value {Path}:{Name}",
                    registryItem.Path, registryItem.Name);
                ServiceTracker.Current?.Track(nameof(Write), false);
                return false;
            }
        }, true, true);
    }

    public static bool DeleteValue(RegistryItem item)
    {
        return WithKey(item, key =>
        {
            try
            {
                if (key.GetValue(item.Name) == null)
                {
                    //ServiceTracker.Current?.Log.LogWarning("Attempted to delete non-existent registry value {Path}:{Name}", item.Path, item.Name);
                    ServiceTracker.Current?.Track(nameof(DeleteValue), true);
                    return true; // after all, we try to delete this value.
                }

                key.DeleteValue(item.Name!, false);
                ServiceTracker.Current?.Track(nameof(DeleteValue), true);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                ServiceTracker.Current?.Log.LogError(
                    "Failed to delete registry value {Path}:{Name} due to missing access", item.Path,
                    item.Name);
                ServiceTracker.Current?.Track(nameof(DeleteValue), false);
                return false;
            }
            catch (Exception ex)
            {
                ServiceTracker.Current?.Log.LogError(ex, "Failed to delete registry value {Path}:{Name}", item.Path,
                    item.Name);
                ServiceTracker.Current?.Track(nameof(DeleteValue), false);
                return false;
            }
        }, true);
    }

    public static bool DeleteSubKey(RegistryItem item)
    {
        if (!TryParsePath(item.Path, out var rootKey, out var subPath))
            return false;

        if (string.IsNullOrWhiteSpace(subPath))
        {
            ServiceTracker.Current?.Log.LogError("DeleteSubKey called with empty subpath! {Path}:{Name}", item.Path,
                item.Name);
            ServiceTracker.Current?.Track(nameof(DeleteSubKey), false);
            return false;
        }

        try
        {
            rootKey.DeleteSubKeyTree(subPath, false);
            ServiceTracker.Current?.Track(nameof(DeleteSubKey), true);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            ServiceTracker.Current?.Log.LogError("Failed to delete subkey {Path} due to missing access", item.Path);
            ServiceTracker.Current?.Track(nameof(DeleteSubKey), false);
            return false;
        }
        catch (Exception ex)
        {
            ServiceTracker.Current?.Log.LogError(ex, "Failed to delete subkey {Path}", item.Path);
            ServiceTracker.Current?.Track(nameof(DeleteSubKey), false);
            return false;
        }
    }

    public static void Write(params RegistryItem[] items)
    {
        foreach (var item in items.Distinct()) Write(item);
    }

    public static void DeleteValue(params RegistryItem[] items)
    {
        foreach (var item in items.Distinct()) DeleteValue(item);
    }

    public static void DeleteSubKey(params RegistryItem[] items)
    {
        foreach (var item in items.Distinct()) DeleteSubKey(item);
    }
}