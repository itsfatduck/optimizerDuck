using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Security;
using Microsoft.Win32;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Services.OptimizationServices;

public static class RegistryService
{
    private static readonly AsyncLocal<string?> _lastError = new();
    private static readonly AsyncLocal<string?> _lastErrorDetail = new();

    internal static string? LastError => _lastError.Value;
    internal static string? LastErrorDetail => _lastErrorDetail.Value;

    private static readonly Dictionary<string, RegistryKey> RootKeysMap = new(
        StringComparer.OrdinalIgnoreCase
    )
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
        ["HKEY_CURRENT_CONFIG"] = Registry.CurrentConfig,
    };

    private static bool TryParsePath(
        string fullPath,
        [NotNullWhen(true)] out RegistryKey? rootKey,
        [NotNullWhen(true)] out string? subPath
    )
    {
        rootKey = null;
        subPath = null;

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            _lastError.Value = _lastErrorDetail.Value = "Registry path is null or empty";
            ExecutionScope.LogError(null, "Registry path is null or empty");
            ExecutionScope.Track(nameof(RegistryService), false);
            return false;
        }

        var pathSpan = fullPath.AsSpan();
        var idx = pathSpan.IndexOf('\\');
        var rootToken = (idx > 0 ? pathSpan[..idx] : pathSpan).ToString();

        if (!RootKeysMap.TryGetValue(rootToken, out rootKey))
        {
            _lastError.Value = _lastErrorDetail.Value = $"Unknown root key: {rootToken}";
            ExecutionScope.LogError(null, "Unknown root key: {RootToken}", rootToken);
            ExecutionScope.Track(nameof(RegistryService), false);
            return false;
        }

        subPath = idx > 0 ? pathSpan[(idx + 1)..].ToString() : string.Empty;
        return true;
    }

    private static bool TryOpenSubKey(
        RegistryKey rootKey,
        string? subPath,
        out RegistryKey? key,
        out bool shouldDispose,
        bool writable,
        bool createIfMissing,
        List<string>? createdSubKeys
    )
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
            var opened = rootKey.OpenSubKey(subPath, writable);

            if (opened == null && writable && createIfMissing)
                opened =
                    createdSubKeys != null
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
                ex
            );
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            TrackRegistryError(
                Translations.Service_Registry_Error_UnauthorizedAccess,
                "Unauthorized access",
                rootKey,
                subPath,
                ex
            );
            return false;
        }
        catch (Exception ex)
        {
            TrackRegistryError(
                Translations.Service_Registry_Error_CreateOrOpenSubkeyFailed,
                "Failed to create/open subkey",
                rootKey,
                subPath,
                ex
            );
            return false;
        }
    }

    private static T? WithKey<T>(
        RegistryItem item,
        Func<RegistryKey, T?> action,
        bool writable = false,
        bool createIfMissing = false,
        List<string>? createdSubKeys = null
    )
    {
        if (!TryParsePath(item.Path, out var rootKey, out var subPath))
            return default;

        if (
            !TryOpenSubKey(
                rootKey,
                subPath,
                out var subKey,
                out var shouldDispose,
                writable,
                createIfMissing,
                createdSubKeys
            )
        )
            return default;

        try
        {
            return subKey == null ? default : action(subKey);
        }
        finally
        {
            if (shouldDispose)
                subKey?.Dispose();
        }
    }

    private static string NormalizeValueName(string? name)
    {
        return name ?? string.Empty;
    }

    /// <summary>Determines whether the specified registry key path exists.</summary>
    /// <param name="item">The registry path to check.</param>
    /// <returns><see langword="true" /> if the key exists; otherwise, <see langword="false" />.</returns>
    public static bool KeyExists(RegistryItem item)
    {
        return WithKey(item, key => true, false);
    }

    /// <summary>Reads a registry value and converts it to the specified type.</summary>
    /// <typeparam name="T">The target type to convert the value to.</typeparam>
    /// <param name="item">The registry path and value name to read.</param>
    /// <returns>The converted value, or the default of <typeparamref name="T" /> if the value is missing or conversion fails.</returns>
    public static T? Read<T>(RegistryItem item)
    {
        return WithKey(
            item,
            key =>
            {
                try
                {
                    var valueName = NormalizeValueName(item.Name);
                    var value = key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames
                    );
                    if (value is null)
                        return default;

                    var result = ConvertRegistryValue<T>(value);
                    ExecutionScope.LogInfo(
                        "Read registry {Path}:{Name} = {Value}",
                        item.Path,
                        item.Name!,
                        result?.ToString() ?? "<null>"
                    );
                    return result;
                }
                catch (Exception ex)
                {
                    ExecutionScope.LogError(
                        ex,
                        "Failed to read registry {Path}:{Name}",
                        item.Path,
                        item.Name!
                    );
                    return default;
                }
            }
        );
    }

    /// <summary>Writes a value to the registry, backing up the previous value for revert.</summary>
    /// <param name="item">The registry path, value name, value, and kind to write.</param>
    /// <returns><see langword="true" /> if the write succeeded; otherwise, <see langword="false" />.</returns>
    public static bool Write(RegistryItem item)
    {
        _lastError.Value = _lastErrorDetail.Value = null;

        if (item.Value == null)
        {
            _lastError.Value = _lastErrorDetail.Value =
                $"Value cannot be null when writing {item.Path}:{item.Name}";
            ExecutionScope.LogError(
                null,
                "Value can't be null when writing {Path}:{Name}",
                item.Path,
                item.Name!
            );
            ExecutionScope.Track(nameof(Write), false);
            return false;
        }

        var createdSubKeys = new List<string>();

        return WithKey(
            item,
            key =>
            {
                var description = string.Format(
                    Translations.Service_Registry_Description_Write,
                    item.Path,
                    item.Name
                );
                try
                {
                    var valueName = NormalizeValueName(item.Name);
                    var backupValue = key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames
                    );
                    var valueExists =
                        backupValue != null
                        || key.GetValueNames()
                            .Contains(valueName, StringComparer.OrdinalIgnoreCase);
                    var backupKind = valueExists
                        ? key.GetValueKind(valueName)
                        : RegistryValueKind.Unknown;

                    key.SetValue(valueName, item.Value, item.Kind);

                    var revertStep = new RegistryRevertStep
                    {
                        Action = valueExists
                            ? RevertAction.RestorePrevious
                            : RevertAction.NoPreviousValue,
                        Path = item.Path,
                        Name = item.Name,
                        Value = backupValue,
                        Kind = backupKind,
                        CreatedSubKeys = createdSubKeys,
                    };

                    ExecutionScope.LogInfo(
                        "Wrote {Path}:{Name}[{Kind}] = {Value}",
                        item.Path,
                        item.Name!,
                        item.Kind,
                        item.Value
                    );
                    ExecutionScope.Track(nameof(Write), true);
                    ExecutionScope.RecordStep(
                        Translations.Service_Registry_Name,
                        description,
                        true,
                        revertStep
                    );
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    _lastError.Value = Translations.Service_Common_Error_AccessDenied;
                    _lastErrorDetail.Value = string.Format(
                        Translations.Service_Registry_ErrorDetail_AccessDeniedWrite,
                        item.Path,
                        item.Name
                    );
                    ExecutionScope.LogError(
                        null,
                        "Access denied writing {Path}:{Name}",
                        item.Path,
                        item.Name!
                    );
                    ExecutionScope.Track(nameof(Write), false);
                    ExecutionScope.RecordStep(
                        Translations.Service_Registry_Name,
                        description,
                        false,
                        null,
                        _lastError.Value,
                        () => Task.FromResult(Write(item)),
                        _lastErrorDetail.Value
                    );
                    return false;
                }
                catch (Exception ex)
                {
                    _lastError.Value = ex.Message;
                    _lastErrorDetail.Value = ex.ToString();
                    ExecutionScope.LogError(
                        ex,
                        "Failed to write registry {Path}:{Name}",
                        item.Path,
                        item.Name!
                    );
                    ExecutionScope.Track(nameof(Write), false);
                    ExecutionScope.RecordStep(
                        Translations.Service_Registry_Name,
                        description,
                        false,
                        null,
                        _lastError.Value,
                        () => Task.FromResult(Write(item)),
                        _lastErrorDetail.Value
                    );
                    return false;
                }
            },
            true,
            true,
            createdSubKeys
        );
    }

    /// <summary>Deletes a registry value, backing up the current value for revert.</summary>
    /// <param name="item">The registry path and value name to delete.</param>
    /// <returns><see langword="true" /> if the value was deleted or did not exist; otherwise, <see langword="false" />.</returns>
    public static bool DeleteValue(RegistryItem item)
    {
        _lastError.Value = _lastErrorDetail.Value = null;

        return WithKey(
            item,
            key =>
            {
                var description = string.Format(
                    Translations.Service_Registry_Description_Delete,
                    item.Path,
                    item.Name ?? "(Default)"
                );
                try
                {
                    var valueName = NormalizeValueName(item.Name);
                    var backupValue = key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames
                    );
                    if (
                        backupValue == null
                        && !key.GetValueNames()
                            .Contains(valueName, StringComparer.OrdinalIgnoreCase)
                    )
                    {
                        ExecutionScope.LogInfo(
                            "Skip delete registry {Path}:{Name} (not found)",
                            item.Path,
                            item.Name!
                        );
                        ExecutionScope.Track(nameof(DeleteValue), true);
                        ExecutionScope.RecordStep(
                            Translations.Service_Registry_Name,
                            description,
                            true
                        );
                        return true;
                    }

                    var backupKind = key.GetValueKind(valueName);
                    key.DeleteValue(valueName, false);

                    var revertStep = new RegistryRevertStep
                    {
                        Action = RevertAction.RestorePrevious,
                        Path = item.Path,
                        Name = item.Name,
                        Value = backupValue,
                        Kind = backupKind,
                    };

                    ExecutionScope.LogInfo("Deleted registry {Path}:{Name}", item.Path, item.Name!);
                    ExecutionScope.Track(nameof(DeleteValue), true);
                    ExecutionScope.RecordStep(
                        Translations.Service_Registry_Name,
                        description,
                        true,
                        revertStep
                    );
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    _lastError.Value = Translations.Service_Common_Error_AccessDenied;
                    _lastErrorDetail.Value = string.Format(
                        Translations.Service_Registry_ErrorDetail_AccessDeniedDelete,
                        item.Path,
                        item.Name
                    );
                    ExecutionScope.LogError(
                        null,
                        "Access denied deleting {Path}:{Name}",
                        item.Path,
                        item.Name!
                    );
                    ExecutionScope.Track(nameof(DeleteValue), false);
                    ExecutionScope.RecordStep(
                        Translations.Service_Registry_Name,
                        description,
                        false,
                        null,
                        _lastError.Value,
                        () => Task.FromResult(DeleteValue(item)),
                        _lastErrorDetail.Value
                    );
                    return false;
                }
                catch (Exception ex)
                {
                    _lastError.Value = ex.Message;
                    _lastErrorDetail.Value = ex.ToString();
                    ExecutionScope.LogError(
                        ex,
                        "Failed to delete registry {Path}:{Name}",
                        item.Path,
                        item.Name!
                    );
                    ExecutionScope.Track(nameof(DeleteValue), false);
                    ExecutionScope.RecordStep(
                        Translations.Service_Registry_Name,
                        description,
                        false,
                        null,
                        _lastError.Value,
                        () => Task.FromResult(DeleteValue(item)),
                        _lastErrorDetail.Value
                    );
                    return false;
                }
            },
            true
        );
    }

    /// <summary>Creates a registry subkey, tracking created intermediate keys for revert.</summary>
    /// <param name="item">The registry path of the key to create.</param>
    /// <returns><see langword="true" /> if the key was created or already exists; otherwise, <see langword="false" />.</returns>
    public static bool CreateSubKey(RegistryItem item)
    {
        _lastError.Value = _lastErrorDetail.Value = null;

        if (!TryParsePath(item.Path, out var rootKey, out var subPath))
            return false;

        var description = string.Format(
            Translations.Service_Registry_Description_CreateKey,
            item.Path
        );
        var createdSubKeys = new List<string>();

        try
        {
            using var key = rootKey.OpenSubKey(subPath, false);
            if (key != null)
            {
                ExecutionScope.LogInfo("Skip create registry {Path} (already exists)", item.Path);
                ExecutionScope.Track(nameof(CreateSubKey), true);
                ExecutionScope.RecordStep(Translations.Service_Registry_Name, description, true);
                return true;
            }

            using var newKey = CreateSubKeyTrack(rootKey, subPath, createdSubKeys);

            var revertStep = new RegistryRevertStep
            {
                Action = RevertAction.NoPreviousValue,
                Path = item.Path,
                Name = null,
                CreatedSubKeys = createdSubKeys,
            };

            ExecutionScope.LogInfo("Created registry key {Path}", item.Path);
            ExecutionScope.Track(nameof(CreateSubKey), true);
            ExecutionScope.RecordStep(
                Translations.Service_Registry_Name,
                description,
                true,
                revertStep
            );
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _lastError.Value = Translations.Service_Common_Error_AccessDenied;
            _lastErrorDetail.Value = string.Format(
                Translations.Service_Registry_ErrorDetail_AccessDeniedCreateKey,
                item.Path
            );
            ExecutionScope.LogError(null, "Access denied creating {Path}", item.Path);
            ExecutionScope.Track(nameof(CreateSubKey), false);
            ExecutionScope.RecordStep(
                Translations.Service_Registry_Name,
                description,
                false,
                null,
                _lastError.Value,
                () => Task.FromResult(CreateSubKey(item)),
                _lastErrorDetail.Value
            );
            return false;
        }
        catch (Exception ex)
        {
            _lastError.Value = ex.Message;
            _lastErrorDetail.Value = ex.ToString();
            ExecutionScope.LogError(ex, "Failed to create registry {Path}", item.Path);
            ExecutionScope.Track(nameof(CreateSubKey), false);
            ExecutionScope.RecordStep(
                Translations.Service_Registry_Name,
                description,
                false,
                null,
                _lastError.Value,
                () => Task.FromResult(CreateSubKey(item)),
                _lastErrorDetail.Value
            );
            return false;
        }
    }

    /// <summary>Deletes an entire registry key tree, backing up all values and subkeys for revert.</summary>
    /// <param name="item">The registry path of the key tree to delete.</param>
    /// <returns><see langword="true" /> if the tree was deleted or did not exist; otherwise, <see langword="false" />.</returns>
    public static bool DeleteSubKeyTree(RegistryItem item)
    {
        _lastError.Value = _lastErrorDetail.Value = null;

        if (!TryParsePath(item.Path, out var rootKey, out var subPath))
            return false;

        var description = string.Format(
            Translations.Service_Registry_Description_DeleteKey,
            item.Path
        );

        try
        {
            using var key = rootKey.OpenSubKey(subPath, false);
            if (key == null)
            {
                ExecutionScope.LogInfo("Skip delete registry key {Path} (not found)", item.Path);
                ExecutionScope.Track(nameof(DeleteSubKeyTree), true);
                ExecutionScope.RecordStep(Translations.Service_Registry_Name, description, true);
                return true;
            }

            var (subSteps, backupComplete) = BackupRegistryTree(key, item.Path);
            if (!backupComplete)
            {
                var msg = string.Format(
                    Translations.Service_Registry_Error_BackupTruncated,
                    item.Path
                );
                _lastError.Value = _lastErrorDetail.Value = msg;
                ExecutionScope.LogError(
                    null,
                    "Registry subtree backup truncated for {Path}",
                    item.Path
                );
                ExecutionScope.Track(nameof(DeleteSubKeyTree), false);
                ExecutionScope.RecordStep(
                    Translations.Service_Registry_Name,
                    description,
                    false,
                    null,
                    _lastError.Value,
                    () => Task.FromResult(DeleteSubKeyTree(item))
                );
                return false;
            }

            rootKey.DeleteSubKeyTree(subPath, false);

            var revertStep = new RegistryRevertStep
            {
                Action = RevertAction.RestoreKeyTree,
                Path = item.Path,
                SubSteps = subSteps,
            };

            ExecutionScope.LogInfo("Deleted registry key tree {Path}", item.Path);
            ExecutionScope.Track(nameof(DeleteSubKeyTree), true);
            ExecutionScope.RecordStep(
                Translations.Service_Registry_Name,
                description,
                true,
                revertStep
            );
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _lastError.Value = Translations.Service_Registry_Error_AccessDeniedProtectedHive;
            _lastErrorDetail.Value = string.Format(
                Translations.Service_Registry_ErrorDetail_AccessDeniedDeleteKeyTree,
                item.Path
            );
            ExecutionScope.LogError(null, "Access denied deleting {Path}", item.Path);
            ExecutionScope.Track(nameof(DeleteSubKeyTree), false);
            ExecutionScope.RecordStep(
                Translations.Service_Registry_Name,
                description,
                false,
                null,
                _lastError.Value,
                () => Task.FromResult(DeleteSubKeyTree(item)),
                _lastErrorDetail.Value
            );
            return false;
        }
        catch (Exception ex)
        {
            _lastError.Value = ex.Message;
            _lastErrorDetail.Value = ex.ToString();
            ExecutionScope.LogError(ex, "Failed to delete subkey tree {Path}", item.Path);
            ExecutionScope.Track(nameof(DeleteSubKeyTree), false);
            ExecutionScope.RecordStep(
                Translations.Service_Registry_Name,
                description,
                false,
                null,
                _lastError.Value,
                () => Task.FromResult(DeleteSubKeyTree(item)),
                _lastErrorDetail.Value
            );
            return false;
        }
    }

    private static (List<RegistryRevertStep> Steps, bool IsComplete) BackupRegistryTree(
        RegistryKey key,
        string keyPath
    )
    {
        var steps = new List<RegistryRevertStep>();
        var truncated = false;
        BackupRegistryTreeRecursive(key, keyPath, steps, 0, ref truncated);
        return (steps, !truncated);
    }

    // walks the full subkey tree to snapshot every value before deletion
    // depth and count limits prevent runaway recursion on massive hives
    private static void BackupRegistryTreeRecursive(
        RegistryKey key,
        string keyPath,
        List<RegistryRevertStep> steps,
        int depth,
        ref bool truncated
    )
    {
        if (depth > 15 || steps.Count > 5000)
        {
            truncated = true;
            ExecutionScope.LogWarning(
                "Registry subtree backup limit reached at {Path} (depth: {Depth}, items: {Count})",
                keyPath,
                depth,
                steps.Count
            );
            return;
        }

        steps.Add(new RegistryRevertStep { Action = RevertAction.RestoreKey, Path = keyPath });

        foreach (var valueName in key.GetValueNames())
        {
            if (steps.Count > 5000)
            {
                truncated = true;
                return;
            }

            steps.Add(
                new RegistryRevertStep
                {
                    Action = RevertAction.RestorePrevious,
                    Path = keyPath,
                    Name = string.IsNullOrEmpty(valueName) ? null : valueName,
                    Value = key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames
                    ),
                    Kind = key.GetValueKind(valueName),
                }
            );
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            if (steps.Count > 5000)
            {
                truncated = true;
                return;
            }

            using var subKey = key.OpenSubKey(subKeyName, false);
            if (subKey != null)
                BackupRegistryTreeRecursive(
                    subKey,
                    $@"{keyPath}\{subKeyName}",
                    steps,
                    depth + 1,
                    ref truncated
                );
        }
    }

    /// <summary>Writes multiple distinct registry values.</summary>
    /// <param name="items">The registry items to write.</param>
    public static void Write(params RegistryItem[] items)
    {
        foreach (var item in items.Distinct())
            Write(item);
    }

    /// <summary>Deletes multiple distinct registry values.</summary>
    /// <param name="items">The registry items to delete.</param>
    public static void DeleteValue(params RegistryItem[] items)
    {
        foreach (var item in items.Distinct())
            DeleteValue(item);
    }

    #region Helpers

    private static T? ConvertRegistryValue<T>(object value)
    {
        // Fast path
        if (value is T t)
            return t;

        var tType = typeof(T);
        var targetType = Nullable.GetUnderlyingType(tType) ?? tType;

        // Arrays often from Registry
        if (targetType == typeof(byte[]) && value is byte[] bytes)
            return (T)(object)bytes;

        if (targetType == typeof(string[]) && value is string[] arr)
            return (T)(object)arr;

        // Bool: Registry sometimes saves DWORD 0/1 or string "true/false", "1"/"0", "yes"/"no"
        if (targetType == typeof(bool))
            return value switch
            {
                int i => (T)(object)(i != 0),
                long l => (T)(object)(l != 0),
                string s when bool.TryParse(s, out var b) => (T)(object)b,
                string s
                    when s.Equals("1") || s.Equals("yes", StringComparison.OrdinalIgnoreCase) => (T)
                    (object)true,
                string s when s.Equals("0") || s.Equals("no", StringComparison.OrdinalIgnoreCase) =>
                    (T)(object)false,
                _ => default,
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
        Exception ex
    )
    {
        var path = string.IsNullOrEmpty(subPath) ? root.Name : $"{root.Name}\\{subPath}";
        _lastError.Value = _lastErrorDetail.Value = uiReason;

        ExecutionScope.LogError(ex, "{Reason}: {Path}", logReason, path);

        ExecutionScope.Track(nameof(RegistryService), false);

        ExecutionScope.RecordStep(Translations.Service_Registry_Name, path, false, null, uiReason);
    }

    // walks each segment of the subkey path, creating missing segments one by one
    // only tracks the keys it actually creates so revert cleanup knows what to delete
    private static RegistryKey CreateSubKeyTrack(
        RegistryKey root,
        string subPath,
        List<string> createdSubKeys
    )
    {
        var parts = subPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        var current = root;
        var ownsCurrent = false;
        var currentPath = string.Empty;

        try
        {
            foreach (var part in parts)
            {
                currentPath = currentPath.Length == 0 ? part : $"{currentPath}\\{part}";

                var next = current.OpenSubKey(part, true);
                if (next == null)
                {
                    next =
                        current.CreateSubKey(part, true)
                        ?? throw new InvalidOperationException(
                            $"Failed to create registry key: {root.Name}\\{currentPath}"
                        );

                    createdSubKeys.Add($"{root.Name}\\{currentPath}");
                    ExecutionScope.LogDebug(
                        "Created registry subkey: {Path}",
                        $"{root.Name}\\{currentPath}"
                    );
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

    /// <summary>Removes empty registry keys that were created during an apply operation.</summary>
    /// <remarks>Only deletes keys that were created during apply (tracked in <paramref name="createdSubKeys"/>) and are now completely empty — no values and no subkeys. Sorts by path depth descending so child keys are deleted before parents.</remarks>
    /// <param name="createdSubKeys">The list of registry key paths that were created.</param>
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

                    ExecutionScope.LogInfo("Cleaned up empty registry key {Path}", fullPath);
                }
                else
                {
                    // Key is not empty, don't delete it
                    ExecutionScope.LogDebug(
                        "Skipped cleanup of registry key {Path} (has {SubKeyCount} subkeys and {ValueCount} values)",
                        fullPath,
                        key.SubKeyCount,
                        key.ValueCount
                    );
                }
            }
            catch (UnauthorizedAccessException)
            {
                ExecutionScope.LogWarning(
                    "Access denied cleaning up registry key: {Path}",
                    fullPath
                );
            }
            catch (IOException ex)
            {
                ExecutionScope.LogError(ex, "I/O error cleaning up registry key: {Path}", fullPath);
            }
            catch (Exception ex)
            {
                ExecutionScope.LogError(ex, "Failed to cleanup registry key: {Path}", fullPath);
            }
    }

    #endregion Helpers
}
