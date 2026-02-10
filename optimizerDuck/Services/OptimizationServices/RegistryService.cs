using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Security;
using Microsoft.Win32;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.Revert.Steps;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Services.OptimizationServices;

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
            ServiceTracker.LogError(null, "Registry path is null or empty");
            ServiceTracker.Track(nameof(RegistryService), false);
            return false;
        }

        var parts = fullPath.AsSpan();
        var idx = parts.IndexOf('\\');
        var rootToken = idx > 0 ? parts[..idx].ToString() : parts.ToString();

        if (!RootKeysMap.TryGetValue(rootToken, out rootKey))
        {
            ServiceTracker.LogError(null, "Unknown root key: {RootToken}", rootToken);
            ServiceTracker.Track(nameof(RegistryService), false);
            return false;
        }

        subPath = idx > 0 ? parts[(idx + 1)..].ToString() : string.Empty;
        return true;
    }

    private static bool TryOpenSubKey(
        RegistryKey rootKey,
        string? subPath,
        out RegistryKey? key,
        out bool shouldDispose,
        bool writable,
        bool createIfMissing,
        List<string>? createdSubKeys)
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
                opened = createdSubKeys != null
                    ? CreateSubKeyTrack(rootKey, subPath, createdSubKeys)
                    : rootKey.CreateSubKey(subPath);

            if (opened != null)
            {
                key = opened;
                shouldDispose = true;
                return true;
            }

            return false;
        }
        catch (SecurityException ex)
        {
            TrackRegistryError(
                Translations.Service_Registry_Error_AccessDeniedProtectedHive,
                "Access denied (protected hive)",
                rootKey,
                subPath,
                ex);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            TrackRegistryError(
                Translations.Service_Registry_Error_UnauthorizedAccess,
                "Unauthorized access",
                rootKey,
                subPath,
                ex);
            return false;
        }
        catch (Exception ex)
        {
            TrackRegistryError(
                Translations.Service_Registry_Error_CreateOrOpenSubkeyFailed,
                "Failed to create/open subkey",
                rootKey,
                subPath,
                ex);
            return false;
        }
    }

    private static T? WithKey<T>(RegistryItem item, Func<RegistryKey, T?> action, bool writable = false,
        bool createIfMissing = false, List<string>? createdSubKeys = null)
    {
        if (!TryParsePath(item.Path, out var rootKey, out var subPath))
            return default;

        if (!TryOpenSubKey(rootKey, subPath, out var subKey, out var shouldDispose, writable, createIfMissing,
                createdSubKeys))
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
            object? value = null;
            try
            {
                value = key.GetValue(item.Name, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value is null) return default;

                var result = ConvertRegistryValue<T>(value);

                ServiceTracker.LogInfo("Read registry {Path}:{Name} = {Value}",
                    item.Path, item.Name!, result);

                return result;
            }
            catch (Exception ex)
            {
                ServiceTracker.LogError(ex, "Failed to read registry {Path}:{Name}",
                    item.Path, item.Name!);

                ServiceTracker.LogInfo("Read registry {Path}:{Name} = <default> (raw={RawType})",
                    item.Path, item.Name!, value?.GetType().FullName);

                return default;
            }
        });
    }

    public static bool Write(RegistryItem item)
    {
        if (item.Value == null)
        {
            ServiceTracker.LogError(null, "Value can't be null when writing {Path}:{Name}",
                item.Path, item.Name!);
            ServiceTracker.Track(nameof(Write), false);
            return false;
        }

        var createdSubKeys = new List<string>();

        return WithKey(item, key =>
        {
            var description = $"{item.Path}:{item.Name}";
            try
            {
                object? backupValue = null;
                RegistryValueKind? backupKind = null;
                var valueExists = false;

                // Detect value existence correctly:
                // - backupValue == null does NOT mean value does not exist
                // - must check value names explicitly
                try
                {
                    backupValue = key.GetValue(item.Name, null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);

                    // Value does not exist if backupValue is null and name not found
                    if (backupValue == null &&
                        !key.GetValueNames().Contains(item.Name))
                    {
                        backupKind = null;
                        valueExists = false; // value does not exist
                    }
                    else
                    {
                        backupKind = key.GetValueKind(item.Name);
                        valueExists = true; // value exists
                    }
                }
                catch
                {
                    backupValue = null;
                    backupKind = null;
                    valueExists = false;
                }

                key.SetValue(item.Name, item.Value, item.Kind);

                if (valueExists)
                    RevertManager.Record(new RegistryRevertStep
                    {
                        Action = RevertAction.RestorePrevious,
                        Path = item.Path,
                        Name = item.Name,
                        Value = backupValue,
                        Kind = backupKind ?? RegistryValueKind.Unknown,
                        CreatedSubKeys = createdSubKeys
                    });
                else
                    RevertManager.Record(new RegistryRevertStep
                    {
                        Action = RevertAction.NoPreviousValue,
                        Path = item.Path,
                        Name = item.Name,
                        CreatedSubKeys = createdSubKeys
                    });

                ServiceTracker.LogInfo("Wrote {Path}:{Name}[{Kind}] = {Value}",
                    item.Path, item.Name!, item.Kind, item.Value);
                ServiceTracker.Track(nameof(Write), true);
                ServiceTracker.TrackStep(
                    "Registry",
                    description,
                    true,
                    null,
                    () => Task.Run(() => Write(item)));
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                ServiceTracker.LogError(null, "Access denied writing {Path}:{Name}",
                    item.Path, item.Name!);
                ServiceTracker.Track(nameof(Write), false);
                ServiceTracker.TrackStep(
                    "Registry",
                    description,
                    false,
                    Translations.Service_Common_Error_AccessDenied,
                    () => Task.Run(() => Write(item)));
                return false;
            }
            catch (Exception ex)
            {
                ServiceTracker.LogError(ex, "Failed to write registry {Path}:{Name}",
                    item.Path, item.Name!);
                ServiceTracker.Track(nameof(Write), false);
                ServiceTracker.TrackStep(
                    "Registry",
                    description,
                    false,
                    ex.Message,
                    () => Task.Run(() => Write(item)));
                return false;
            }
        }, true, true, createdSubKeys);
    }

    public static bool DeleteValue(RegistryItem item)
    {
        if (!TryParsePath(item.Path, out var rootKey, out var subPath))
            return false;

        RegistryKey? key = null;
        var shouldDispose = false;

        try
        {
            var description = $"{item.Path}:{item.Name}";
            if (string.IsNullOrEmpty(subPath))
            {
                key = rootKey;
            }
            else
            {
                key = rootKey.OpenSubKey(subPath, true);
                if (key == null)
                {
                    // Missing subkey means there's nothing to delete; treat as success for revert.
                    ServiceTracker.LogInfo("Skip delete registry {Path}:{Name} (subkey missing)",
                        item.Path, item.Name!);
                    ServiceTracker.Track(nameof(DeleteValue), true);
                    ServiceTracker.TrackStep(
                        "Registry",
                        description,
                        true,
                        null,
                        () => Task.Run(() => DeleteValue(item)));
                    return true;
                }

                shouldDispose = true;
            }

            // Key does not exist, treat as success for revert
            if (key.GetValue(item.Name) == null)
            {
                ServiceTracker.Track(nameof(DeleteValue), true);
                ServiceTracker.TrackStep(
                    "Registry",
                    description,
                    true,
                    null,
                    () => Task.Run(() => DeleteValue(item)));
                return true;
            }

            var backupValue = key.GetValue(item.Name);
            var backupKind = key.GetValueKind(item.Name);

            key.DeleteValue(item.Name!, false);

            // Record revert: key existed before deletion
            RevertManager.Record(new RegistryRevertStep
            {
                Action = RevertAction.RestorePrevious,
                Path = item.Path,
                Name = item.Name,
                Value = backupValue,
                Kind = backupKind
            });

            ServiceTracker.LogInfo("Deleted registry {Path}:{Name}", item.Path, item.Name!);
            ServiceTracker.Track(nameof(DeleteValue), true);
            ServiceTracker.TrackStep(
                "Registry",
                description,
                true,
                null,
                () => Task.Run(() => DeleteValue(item)));
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            ServiceTracker.LogError(null, "Access denied deleting {Path}:{Name}",
                item.Path, item.Name!);
            ServiceTracker.Track(nameof(DeleteValue), false);
            ServiceTracker.TrackStep(
                "Registry",
                $"{item.Path}:{item.Name}",
                false,
                Translations.Service_Common_Error_AccessDenied,
                () => Task.Run(() => DeleteValue(item)));
            return false;
        }
        catch (Exception ex)
        {
            ServiceTracker.LogError(ex, "Failed to delete registry {Path}:{Name}",
                item.Path, item.Name!);
            ServiceTracker.Track(nameof(DeleteValue), false);
            ServiceTracker.TrackStep(
                "Registry",
                $"{item.Path}:{item.Name}",
                false,
                ex.Message,
                () => Task.Run(() => DeleteValue(item)));
            return false;
        }
        finally
        {
            if (shouldDispose)
                key?.Dispose();
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

    #region Helpers

    private static T? ConvertRegistryValue<T>(object value)
    {
        // Fast path
        if (value is T t) return t;

        var tType = typeof(T);
        var targetType = Nullable.GetUnderlyingType(tType) ?? tType;

        // Arrays often from Registry
        if (targetType == typeof(byte[]) && value is byte[] bytes)
            return (T)(object)bytes;

        if (targetType == typeof(string[]) && value is string[] arr)
            return (T)(object)arr;

        // Bool: Registry sometimes saves DWORD 0/1 or string "true/false"
        if (targetType == typeof(bool))
            return value switch
            {
                int i => (T)(object)(i != 0),
                long l => (T)(object)(l != 0),
                string s when bool.TryParse(s, out var b) => (T)(object)b,
                _ => default
            };

        // Enum: supports both string and numeric
        if (targetType.IsEnum)
        {
            if (value is string es)
                return (T)Enum.Parse(targetType, es, true);

            var enumUnderlying = Enum.GetUnderlyingType(targetType);
            var num = Convert.ChangeType(value, enumUnderlying, CultureInfo.InvariantCulture);
            return (T)Enum.ToObject(targetType, num!);
        }

        // Numeric/string common conversions
        var converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        return (T)converted!;
    }

    private static void TrackRegistryError(
        string uiReason,
        string logReason,
        RegistryKey root,
        string? subPath,
        Exception ex)
    {
        var path = string.IsNullOrEmpty(subPath)
            ? root.Name
            : $"{root.Name}\\{subPath}";

        ServiceTracker.LogError(
            ex,
            "{Reason}: {Path}",
            logReason,
            path);

        ServiceTracker.Track(nameof(RegistryService), false);

        ServiceTracker.TrackStep(
            "Registry",
            path,
            false,
            uiReason,
            () => Task.FromResult(false));
    }

    /// <summary>
    ///     Creates registry subkeys and tracks which ones were created (did not exist before)
    /// </summary>
    private static RegistryKey CreateSubKeyTrack(
        RegistryKey root,
        string subPath,
        List<string> createdSubKeys)
    {
        var parts = subPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        var current = root;
        var ownsCurrent = false;
        var currentPath = string.Empty;

        try
        {
            foreach (var part in parts)
            {
                currentPath = currentPath.Length == 0
                    ? part
                    : $"{currentPath}\\{part}";

                var next = current.OpenSubKey(part, true);
                if (next == null)
                {
                    next = current.CreateSubKey(part, true)
                           ?? throw new InvalidOperationException(
                               $"Failed to create registry key: {root.Name}\\{currentPath}");

                    createdSubKeys.Add($"{root.Name}\\{currentPath}");
                    ServiceTracker.LogDebug(
                        "Created registry subkey: {Path}",
                        $"{root.Name}\\{currentPath}");
                }

                // dispose old key if we opened it
                if (ownsCurrent)
                    current.Dispose();

                current = next;
                ownsCurrent = true;
            }

            return current;
        }
        catch
        {
            if (ownsCurrent)
                current.Dispose();

            throw;
        }
    }


    /// <summary>
    ///     Safely cleanup empty registry keys that were created during apply operation
    ///     Only deletes keys that:
    ///     1. Were created during the apply operation (in createdSubKeys list)
    ///     2. Are now empty (no values and no subkeys)
    /// </summary>
    public static void CleanupEmptyKeys(IEnumerable<string> createdSubKeys)
    {
        // Sort by path length descending to delete deepest keys first
        var sortedKeys = createdSubKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(k => k.Count(c => c == '\\'))
            .ToList();

        foreach (var fullPath in sortedKeys)
            try
            {
                if (!TryParsePath(fullPath, out var root, out var subPath))
                    continue;

                // Check if the key still exists and is empty
                using var key = root.OpenSubKey(subPath, false);

                if (key == null)
                    // Key already deleted, skip
                    continue;

                // Only delete if the key is completely empty (no values, no subkeys)
                if (key is { SubKeyCount: 0, ValueCount: 0 })
                {
                    // Get parent path and key name
                    var idx = subPath.LastIndexOf('\\');
                    var parentPath = idx > 0 ? subPath[..idx] : string.Empty;
                    var keyName = idx > 0 ? subPath[(idx + 1)..] : subPath;

                    if (string.IsNullOrEmpty(parentPath))
                    {
                        root.DeleteSubKey(keyName, false);
                    }
                    else
                    {
                        using var parent = root.OpenSubKey(parentPath, true);
                        parent?.DeleteSubKey(keyName, false);
                    }

                    ServiceTracker.LogInfo("Cleaned up empty registry key {Path}", fullPath);
                }
                else
                {
                    // Key is not empty, don't delete it
                    ServiceTracker.LogDebug(
                        "Skipped cleanup of registry key {Path} (has {SubKeyCount} subkeys and {ValueCount} values)",
                        fullPath, key.SubKeyCount, key.ValueCount);
                }
            }
            catch (UnauthorizedAccessException)
            {
                ServiceTracker.LogWarning("Access denied cleaning up registry key: {Path}", fullPath);
            }
            catch (IOException ex)
            {
                ServiceTracker.LogError(ex, "I/O error cleaning up registry key: {Path}", fullPath);
            }
            catch (Exception ex)
            {
                ServiceTracker.LogError(ex, "Failed to cleanup registry key: {Path}", fullPath);
            }
    }

    #endregion Helpers
}