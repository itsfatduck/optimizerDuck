using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Revert.Steps;

namespace optimizerDuck.Services.System.Primitives;

public static class RegistryService
{
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
        ILogger? logger,
        [NotNullWhen(true)] out RegistryKey? rootKey,
        [NotNullWhen(true)] out string? subPath
    )
    {
        rootKey = null;
        subPath = null;

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            logger?.LogError("Registry path is null or empty");
            return false;
        }

        var pathSpan = fullPath.AsSpan();
        var idx = pathSpan.IndexOf('\\');
        var rootToken = (idx > 0 ? pathSpan[..idx] : pathSpan).ToString();

        if (!RootKeysMap.TryGetValue(rootToken, out rootKey))
        {
            logger?.LogError("Unknown root key: {RootToken}", rootToken);
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
        List<string>? createdSubKeys,
        ILogger? logger,
        out string? error,
        out string? errorDetail
    )
    {
        key = null;
        shouldDispose = false;
        error = null;
        errorDetail = null;

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
                        ? CreateSubKeyTrack(rootKey, subPath, createdSubKeys, logger)
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
            var path = $"{rootKey.Name}\\{subPath}";
            error = ServiceStrings.RegistryErrorAccessDeniedProtectedHive;
            errorDetail = ex.ToString();
            logger?.LogError(ex, "Access denied (protected hive): {Path}", path);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            var path = $"{rootKey.Name}\\{subPath}";
            error = ServiceStrings.RegistryErrorUnauthorizedAccess;
            errorDetail = ex.ToString();
            logger?.LogError(ex, "Unauthorized access: {Path}", path);
            return false;
        }
        catch (Exception ex)
        {
            var path = $"{rootKey.Name}\\{subPath}";
            error = ServiceStrings.RegistryErrorCreateOrOpenSubkeyFailed;
            errorDetail = ex.ToString();
            logger?.LogError(ex, "Failed to create/open subkey: {Path}", path);
            return false;
        }
    }

    private static T? WithKey<T>(
        RegistryItem item,
        Func<RegistryKey, T?> action,
        ILogger? logger,
        out bool opened,
        out string? openError,
        out string? openErrorDetail,
        bool writable = false,
        bool createIfMissing = false,
        List<string>? createdSubKeys = null
    )
    {
        opened = false;
        openError = null;
        openErrorDetail = null;

        if (!TryParsePath(item.Path, logger, out var rootKey, out var subPath))
            return default;

        if (
            !TryOpenSubKey(
                rootKey,
                subPath,
                out var subKey,
                out var shouldDispose,
                writable,
                createIfMissing,
                createdSubKeys,
                logger,
                out openError,
                out openErrorDetail
            )
        )
            return default;

        opened = true;

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
    /// <param name="logger">Optional logger; <c>null</c> means silent.</param>
    /// <returns><see langword="true" /> if the key exists; otherwise, <see langword="false" />.</returns>
    /// <remarks>A failed query (access denied, unknown root) is reported as "does not exist". Use <see cref="TryKeyExists" /> when that distinction matters.</remarks>
    public static bool KeyExists(RegistryItem item, ILogger? logger = null)
    {
        return WithKey<bool>(item, key => true, logger, out _, out _, out _);
    }

    /// <summary>
    ///     Determines whether the specified registry key exists, distinguishing an absent key
    ///     from a failed query.
    /// </summary>
    /// <param name="exists">Whether the key exists; only meaningful on <c>true</c> return.</param>
    /// <returns><c>false</c> when the query itself failed; absence must not be assumed then.</returns>
    public static bool TryKeyExists(RegistryItem item, out bool exists, ILogger? logger = null)
    {
        exists = false;
        if (!TryParsePath(item.Path, logger, out var rootKey, out var subPath))
            return false;

        if (
            !TryOpenSubKey(
                rootKey,
                subPath,
                out var key,
                out var shouldDispose,
                false,
                false,
                null,
                logger,
                out var openError,
                out _
            )
        )
            return openError is null; // absent key, not a failed query

        try
        {
            exists = true;
            return true;
        }
        finally
        {
            if (shouldDispose)
                key?.Dispose();
        }
    }

    /// <summary>
    ///     Reads a raw registry value, distinguishing an absent value from a failed read.
    /// </summary>
    /// <param name="value">The raw value, or <c>null</c> when absent or unreadable.</param>
    /// <returns><c>false</c> when the read itself failed; a failed read is not "absent".</returns>
    public static bool TryReadValue(RegistryItem item, out object? value, ILogger? logger = null)
    {
        value = null;
        if (!TryParsePath(item.Path, logger, out var rootKey, out var subPath))
            return false;

        if (
            !TryOpenSubKey(
                rootKey,
                subPath,
                out var key,
                out var shouldDispose,
                false,
                false,
                null,
                logger,
                out var openError,
                out _
            )
        )
            // An absent key is a successful query with an absent value; an error is a failed query.
            return openError is null;

        try
        {
            value = key?.GetValue(
                NormalizeValueName(item.Name),
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames
            );
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to read registry {Path}:{Name}", item.Path, item.Name!);
            value = null;
            return false;
        }
        finally
        {
            if (shouldDispose)
                key?.Dispose();
        }
    }

    /// <summary>Reads a registry value and converts it to the specified type.</summary>
    /// <typeparam name="T">The target type to convert the value to.</typeparam>
    /// <param name="item">The registry path and value name to read.</param>
    /// <param name="logger">Optional logger; <c>null</c> means silent.</param>
    /// <returns>The converted value, or the default of <typeparamref name="T" /> if the value is missing or conversion fails.</returns>
    public static T? Read<T>(RegistryItem item, ILogger? logger = null)
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
                    logger?.LogInformation(
                        "Read registry {Path}:{Name} = {Value}",
                        item.Path,
                        item.Name!,
                        result?.ToString() ?? "<null>"
                    );
                    return result;
                }
                catch (Exception ex)
                {
                    logger?.LogError(
                        ex,
                        "Failed to read registry {Path}:{Name}",
                        item.Path,
                        item.Name!
                    );
                    return default;
                }
            },
            logger,
            out _,
            out _,
            out _
        );
    }

    /// <summary>Writes a value to the registry, backing up the previous value for revert.</summary>
    /// <param name="call">The call context carrying the change collector and logger.</param>
    /// <param name="item">The registry path, value name, value, and kind to write.</param>
    /// <returns>The operation result, carrying the revert step on success.</returns>
    public static OpResult Write(OpCall call, RegistryItem item)
    {
        ArgumentNullException.ThrowIfNull(call);
        var name = ServiceStrings.RegistryName;
        var description = ServiceStrings.Format(
            ServiceStrings.RegistryDescriptionWrite,
            item.Path,
            item.Name
        );
        var logger = call.Logger;

        if (item.Value == null)
        {
            var nullError = $"Value cannot be null when writing {item.Path}:{item.Name}";
            logger.LogError(
                "Value can't be null when writing {Path}:{Name}",
                item.Path,
                item.Name!
            );
            return OpResult.Fail(nullError, nullError);
        }

        var createdSubKeys = new List<string>();

        var result = WithKey<OpResult>(
            item,
            regKey =>
            {
                try
                {
                    var valueName = NormalizeValueName(item.Name);
                    var backupValue = regKey.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames
                    );
                    var valueExists =
                        backupValue != null
                        || regKey
                            .GetValueNames()
                            .Contains(valueName, StringComparer.OrdinalIgnoreCase);
                    var backupKind = valueExists
                        ? regKey.GetValueKind(valueName)
                        : RegistryValueKind.Unknown;

                    if (
                        valueExists
                        && backupKind == item.Kind
                        && ValuesEqual(backupValue, item.Value, item.Kind)
                    )
                    {
                        logger.LogInformation(
                            "Skip write registry {Path}:{Name} (already set)",
                            item.Path,
                            item.Name!
                        );
                        call.Changes.AddSkip(name, description);
                        return OpResult.Success();
                    }

                    regKey.SetValue(valueName, item.Value, item.Kind);

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

                    logger.LogInformation(
                        "Wrote {Path}:{Name}[{Kind}] = {Value}",
                        item.Path,
                        item.Name!,
                        item.Kind,
                        item.Value
                    );
                    call.Changes.Add(name, description, true, revertStep);
                    return OpResult.Success(revertStep);
                }
                catch (UnauthorizedAccessException)
                {
                    var error = ServiceStrings.CommonErrorAccessDenied;
                    var errorDetail = ServiceStrings.Format(
                        ServiceStrings.RegistryErrorDetailAccessDeniedWrite,
                        item.Path,
                        item.Name
                    );
                    logger.LogError("Access denied writing {Path}:{Name}", item.Path, item.Name!);
                    call.Changes.Add(
                        name,
                        description,
                        false,
                        null,
                        error,
                        errorDetail,
                        (OpCall rc) => Task.FromResult(Write(rc, item))
                    );
                    return OpResult.Fail(error, errorDetail);
                }
                catch (Exception ex)
                {
                    var error = ex.Message;
                    var errorDetail = ex.ToString();
                    logger.LogError(
                        ex,
                        "Failed to write registry {Path}:{Name}",
                        item.Path,
                        item.Name!
                    );
                    call.Changes.Add(
                        name,
                        description,
                        false,
                        null,
                        error,
                        errorDetail,
                        (OpCall rc) => Task.FromResult(Write(rc, item))
                    );
                    return OpResult.Fail(error, errorDetail);
                }
            },
            logger,
            out _,
            out var openError,
            out var openErrorDetail,
            true,
            true,
            createdSubKeys
        );

        if (result is null)
        {
            if (openError is null)
                return OpResult.Fail($"Failed to access registry {item.Path}:{item.Name}.");
            call.Changes.Add(
                name,
                description,
                false,
                null,
                openError,
                openErrorDetail,
                (OpCall rc) => Task.FromResult(Write(rc, item))
            );
            return OpResult.Fail(openError, openErrorDetail);
        }

        return result;
    }

    /// <summary>Deletes a registry value, backing up the current value for revert.</summary>
    /// <param name="call">The call context carrying the change collector and logger.</param>
    /// <param name="item">The registry path and value name to delete.</param>
    /// <returns>The operation result, carrying the revert step on success.</returns>
    public static OpResult DeleteValue(OpCall call, RegistryItem item)
    {
        ArgumentNullException.ThrowIfNull(call);
        var name = ServiceStrings.RegistryName;
        var description = ServiceStrings.Format(
            ServiceStrings.RegistryDescriptionDelete,
            item.Path,
            item.Name ?? "(Default)"
        );
        var logger = call.Logger;

        var result = WithKey<OpResult>(
            item,
            regKey =>
            {
                try
                {
                    var valueName = NormalizeValueName(item.Name);
                    var backupValue = regKey.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames
                    );
                    if (
                        backupValue == null
                        && !regKey
                            .GetValueNames()
                            .Contains(valueName, StringComparer.OrdinalIgnoreCase)
                    )
                    {
                        logger.LogInformation(
                            "Skip delete registry {Path}:{Name} (not found)",
                            item.Path,
                            item.Name!
                        );
                        call.Changes.AddSkip(name, description);
                        return OpResult.Success();
                    }

                    var backupKind = regKey.GetValueKind(valueName);
                    regKey.DeleteValue(valueName, false);

                    var revertStep = new RegistryRevertStep
                    {
                        Action = RevertAction.RestorePrevious,
                        Path = item.Path,
                        Name = item.Name,
                        Value = backupValue,
                        Kind = backupKind,
                    };

                    logger.LogInformation("Deleted registry {Path}:{Name}", item.Path, item.Name!);
                    call.Changes.Add(name, description, true, revertStep);
                    return OpResult.Success(revertStep);
                }
                catch (UnauthorizedAccessException)
                {
                    var error = ServiceStrings.CommonErrorAccessDenied;
                    var errorDetail = ServiceStrings.Format(
                        ServiceStrings.RegistryErrorDetailAccessDeniedDelete,
                        item.Path,
                        item.Name
                    );
                    logger.LogError("Access denied deleting {Path}:{Name}", item.Path, item.Name!);
                    call.Changes.Add(
                        name,
                        description,
                        false,
                        null,
                        error,
                        errorDetail,
                        (OpCall rc) => Task.FromResult(DeleteValue(rc, item))
                    );
                    return OpResult.Fail(error, errorDetail);
                }
                catch (Exception ex)
                {
                    var error = ex.Message;
                    var errorDetail = ex.ToString();
                    logger.LogError(
                        ex,
                        "Failed to delete registry {Path}:{Name}",
                        item.Path,
                        item.Name!
                    );
                    call.Changes.Add(
                        name,
                        description,
                        false,
                        null,
                        error,
                        errorDetail,
                        (OpCall rc) => Task.FromResult(DeleteValue(rc, item))
                    );
                    return OpResult.Fail(error, errorDetail);
                }
            },
            logger,
            out _,
            out var openError,
            out var openErrorDetail,
            true
        );

        if (result is null)
        {
            if (openError is null)
                return OpResult.Fail($"Failed to access registry {item.Path}:{item.Name}.");
            call.Changes.Add(
                name,
                description,
                false,
                null,
                openError,
                openErrorDetail,
                (OpCall rc) => Task.FromResult(DeleteValue(rc, item))
            );
            return OpResult.Fail(openError, openErrorDetail);
        }

        return result;
    }

    /// <summary>Creates a registry subkey, tracking created intermediate keys for revert.</summary>
    /// <param name="call">The call context carrying the change collector and logger.</param>
    /// <param name="item">The registry path of the key to create.</param>
    /// <returns>The operation result, carrying the revert step on success.</returns>
    public static OpResult CreateSubKey(OpCall call, RegistryItem item)
    {
        ArgumentNullException.ThrowIfNull(call);
        var name = ServiceStrings.RegistryName;
        var description = ServiceStrings.Format(
            ServiceStrings.RegistryDescriptionCreateKey,
            item.Path
        );
        var logger = call.Logger;

        if (!TryParsePath(item.Path, logger, out var rootKey, out var subPath))
            return OpResult.Fail($"Failed to parse registry path: {item.Path}.");

        var createdSubKeys = new List<string>();

        try
        {
            using var regKey = rootKey.OpenSubKey(subPath, false);
            if (regKey != null)
            {
                logger.LogInformation("Skip create registry {Path} (already exists)", item.Path);
                call.Changes.AddSkip(name, description);
                return OpResult.Success();
            }

            using var newKey = CreateSubKeyTrack(rootKey, subPath, createdSubKeys, logger);

            var revertStep = new RegistryRevertStep
            {
                Action = RevertAction.NoPreviousValue,
                Path = item.Path,
                Name = null,
                CreatedSubKeys = createdSubKeys,
            };

            logger.LogInformation("Created registry key {Path}", item.Path);
            call.Changes.Add(name, description, true, revertStep);
            return OpResult.Success(revertStep);
        }
        catch (UnauthorizedAccessException)
        {
            var error = ServiceStrings.CommonErrorAccessDenied;
            var errorDetail = ServiceStrings.Format(
                ServiceStrings.RegistryErrorDetailAccessDeniedCreateKey,
                item.Path
            );
            logger.LogError("Access denied creating {Path}", item.Path);
            call.Changes.Add(
                name,
                description,
                false,
                null,
                error,
                errorDetail,
                (OpCall rc) => Task.FromResult(CreateSubKey(rc, item))
            );
            return OpResult.Fail(error, errorDetail);
        }
        catch (Exception ex)
        {
            var error = ex.Message;
            var errorDetail = ex.ToString();
            logger.LogError(ex, "Failed to create registry {Path}", item.Path);
            call.Changes.Add(
                name,
                description,
                false,
                null,
                error,
                errorDetail,
                (OpCall rc) => Task.FromResult(CreateSubKey(rc, item))
            );
            return OpResult.Fail(error, errorDetail);
        }
    }

    /// <summary>Deletes an entire registry key tree, backing up all values and subkeys for revert.</summary>
    /// <param name="call">The call context carrying the change collector and logger.</param>
    /// <param name="item">The registry path of the key tree to delete.</param>
    /// <returns>The operation result, carrying the revert step on success.</returns>
    public static OpResult DeleteSubKeyTree(OpCall call, RegistryItem item)
    {
        ArgumentNullException.ThrowIfNull(call);
        var name = ServiceStrings.RegistryName;
        var description = ServiceStrings.Format(
            ServiceStrings.RegistryDescriptionDeleteKey,
            item.Path
        );
        var logger = call.Logger;

        if (!TryParsePath(item.Path, logger, out var rootKey, out var subPath))
            return OpResult.Fail($"Failed to parse registry path: {item.Path}.");

        try
        {
            using var regKey = rootKey.OpenSubKey(subPath, false);
            if (regKey == null)
            {
                logger.LogInformation("Skip delete registry key {Path} (not found)", item.Path);
                call.Changes.AddSkip(name, description);
                return OpResult.Success();
            }

            var (subSteps, backupComplete) = BackupRegistryTree(regKey, item.Path, logger);
            if (!backupComplete)
            {
                var error = ServiceStrings.Format(
                    ServiceStrings.RegistryErrorBackupTruncated,
                    item.Path
                );
                logger.LogError("Registry subtree backup truncated for {Path}", item.Path);
                call.Changes.Add(
                    name,
                    description,
                    false,
                    null,
                    error,
                    error,
                    (OpCall rc) => Task.FromResult(DeleteSubKeyTree(rc, item))
                );
                return OpResult.Fail(error, error);
            }

            rootKey.DeleteSubKeyTree(subPath, false);

            var revertStep = new RegistryRevertStep
            {
                Action = RevertAction.RestoreKeyTree,
                Path = item.Path,
                SubSteps = subSteps,
            };

            logger.LogInformation("Deleted registry key tree {Path}", item.Path);
            call.Changes.Add(name, description, true, revertStep);
            return OpResult.Success(revertStep);
        }
        catch (UnauthorizedAccessException)
        {
            var error = ServiceStrings.RegistryErrorAccessDeniedProtectedHive;
            var errorDetail = ServiceStrings.Format(
                ServiceStrings.RegistryErrorDetailAccessDeniedDeleteKeyTree,
                item.Path
            );
            logger.LogError("Access denied deleting {Path}", item.Path);
            call.Changes.Add(
                name,
                description,
                false,
                null,
                error,
                errorDetail,
                (OpCall rc) => Task.FromResult(DeleteSubKeyTree(rc, item))
            );
            return OpResult.Fail(error, errorDetail);
        }
        catch (Exception ex)
        {
            var error = ex.Message;
            var errorDetail = ex.ToString();
            logger.LogError(ex, "Failed to delete subkey tree {Path}", item.Path);
            call.Changes.Add(
                name,
                description,
                false,
                null,
                error,
                errorDetail,
                (OpCall rc) => Task.FromResult(DeleteSubKeyTree(rc, item))
            );
            return OpResult.Fail(error, errorDetail);
        }
    }

    private static (List<RegistryRevertStep> Steps, bool IsComplete) BackupRegistryTree(
        RegistryKey key,
        string keyPath,
        ILogger? logger
    )
    {
        var steps = new List<RegistryRevertStep>();
        var truncated = false;
        BackupRegistryTreeRecursive(key, keyPath, steps, 0, ref truncated, logger);
        return (steps, !truncated);
    }

    // walks the full subkey tree to snapshot every value before deletion
    // depth and count limits prevent runaway recursion on massive hives
    private static void BackupRegistryTreeRecursive(
        RegistryKey key,
        string keyPath,
        List<RegistryRevertStep> steps,
        int depth,
        ref bool truncated,
        ILogger? logger
    )
    {
        if (depth > 15 || steps.Count > 5000)
        {
            truncated = true;
            logger?.LogWarning(
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
                    ref truncated,
                    logger
                );
        }
    }

    /// <summary>Writes multiple distinct registry values, recording one change per item.</summary>
    /// <param name="call">The shared call context for all items.</param>
    /// <param name="items">The registry items to write. All are attempted; the first failure is returned.</param>
    public static OpResult Write(OpCall call, params RegistryItem[] items)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(items);

        // Materialise before aggregating: a lazy sequence would stop at the first failure.
        return OpResult.FirstFailure(items.Distinct().Select(item => Write(call, item)).ToList());
    }

    /// <summary>Deletes multiple distinct registry values, recording one change per item.</summary>
    /// <param name="call">The shared call context for all items.</param>
    /// <param name="items">The registry items to delete. All are attempted; the first failure is returned.</param>
    public static OpResult DeleteValue(OpCall call, params RegistryItem[] items)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(items);

        return OpResult.FirstFailure(
            items.Distinct().Select(item => DeleteValue(call, item)).ToList()
        );
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

    // walks each segment of the subkey path, creating missing segments one by one
    // only tracks the keys it actually creates so revert cleanup knows what to delete
    private static RegistryKey CreateSubKeyTrack(
        RegistryKey root,
        string subPath,
        List<string> createdSubKeys,
        ILogger? logger
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
                    logger?.LogDebug(
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
    /// <remarks>
    ///     Only deletes keys that were created during apply (tracked in
    ///     <paramref name="createdSubKeys" />) and are now completely empty (no values, no
    ///     subkeys). Sorts by path depth descending so child keys are deleted before parents.
    /// </remarks>
    /// <param name="createdSubKeys">The list of registry key paths that were created.</param>
    /// <param name="logger">Optional logger; <c>null</c> means silent.</param>
    public static void CleanupEmptyKeys(IEnumerable<string> createdSubKeys, ILogger? logger = null)
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
                if (!TryParsePath(fullPath, logger, out var root, out var subPath))
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

                    logger?.LogInformation("Cleaned up empty registry key {Path}", fullPath);
                }
                else
                {
                    // Key is not empty, don't delete it
                    logger?.LogDebug(
                        "Skipped cleanup of registry key {Path} (has {SubKeyCount} subkeys and {ValueCount} values)",
                        fullPath,
                        key.SubKeyCount,
                        key.ValueCount
                    );
                }
            }
            catch (UnauthorizedAccessException)
            {
                logger?.LogWarning("Access denied cleaning up registry key: {Path}", fullPath);
            }
            catch (IOException ex)
            {
                logger?.LogError(ex, "I/O error cleaning up registry key: {Path}", fullPath);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to cleanup registry key: {Path}", fullPath);
            }
    }

    private static bool ValuesEqual(object? actual, object? expected, RegistryValueKind kind) =>
        RegistryValues.Equal(actual, expected, kind);

    #endregion Helpers
}
