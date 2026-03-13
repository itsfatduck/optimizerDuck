using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Core.Models.Revert.Steps;

/// <summary>
///     Represents a revert step that restores or deletes a registry value.
/// </summary>
public class RegistryRevertStep : IRevertStep
{
    /// <summary>
    ///     The action to perform (restore previous value or delete).
    /// </summary>
    public RevertAction Action { get; init; }

    /// <summary>
    ///     The registry key path.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    ///     The registry value name, or <c>null</c> for the default value.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     List of subkeys that were created during the apply operation (in order of creation, deepest first for cleanup)
    /// </summary>
    public IReadOnlyList<string>? CreatedSubKeys { get; init; }

    /// <summary>
    ///     Nested sub-steps to perform a complex action like restoring a full registry tree.
    /// </summary>
    public IReadOnlyList<RegistryRevertStep>? SubSteps { get; init; }


    /// <summary>
    ///     The original registry value to restore.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    ///     The registry value kind (DWord, String, etc.).
    /// </summary>
    public RegistryValueKind Kind { get; init; }

    /// <inheritdoc />
    public string Type => "Registry";

    /// <inheritdoc />
    public string Description => Action switch
    {
        RevertAction.RestorePrevious => string.Format(Translations.Revert_Registry_Description_Restore, Path, Name ?? "(Default)"),
        RevertAction.NoPreviousValue => string.Format(Translations.Revert_Registry_Description_Delete, Path, Name ?? "(Default)"),
        RevertAction.RestoreKey => string.Format(Translations.Revert_Registry_Description_RestoreKey, Path),
        RevertAction.DeleteKey => string.Format(Translations.Revert_Registry_Description_DeleteKey, Path),
        RevertAction.RestoreKeyTree => string.Format(Translations.Revert_Registry_Description_RestoreKey, Path),
        _ => $"Revert {Path}"
    };

    /// <inheritdoc />
    public Task<bool> ExecuteAsync()
    {
        var result = Action switch
        {
            RevertAction.NoPreviousValue =>
                RegistryService.DeleteValue(new RegistryItem(Path, Name!)),

            RevertAction.RestorePrevious =>
                Value == null
                    ? RegistryService.DeleteValue(new RegistryItem(Path, Name!))
                    : RegistryService.Write(new RegistryItem(Path, Name!, Value, Kind)),

            RevertAction.RestoreKey =>
                RegistryService.CreateSubKey(new RegistryItem(Path)),

            RevertAction.DeleteKey =>
                RegistryService.DeleteSubKeyTree(new RegistryItem(Path)),

            RevertAction.RestoreKeyTree =>
                ExecuteSubSteps(),

            _ => false
        };

        if (!result)
            throw new Exception(
                string.Format(Translations
                    .Service_Common_Error_AccessDenied)); // Generic for now, but better than nothing

        // Cleanup empty subkeys if they were created during apply
        if (result && CreatedSubKeys?.Count > 0) RegistryService.CleanupEmptyKeys(CreatedSubKeys);

        return Task.FromResult(result);
    }


    /// <inheritdoc />
    public JObject ToData()
    {
        var obj = new JObject
        {
            [nameof(Action)] = Action.ToString(),
            [nameof(Path)] = Path,
            [nameof(Name)] = Name,
            [nameof(Kind)] = Kind.ToString()
        };

        // Save created subkeys list
        if (CreatedSubKeys?.Count > 0) obj[nameof(CreatedSubKeys)] = new JArray(CreatedSubKeys);

        if (SubSteps?.Count > 0)
        {
            var subArray = new JArray();
            foreach (var step in SubSteps) subArray.Add(step.ToData());
            obj[nameof(SubSteps)] = subArray;
        }

        // Serialize Value based on Kind to ensure deterministic JSON and avoid JValue rendering issues
        if (Value == null)
        {
            obj[nameof(Value)] = null;
            return obj;
        }

        switch (Kind)
        {
            case RegistryValueKind.DWord:
                obj[nameof(Value)] = new JValue(Convert.ToInt32(Value));
                break;
            case RegistryValueKind.QWord:
                obj[nameof(Value)] = new JValue(Convert.ToInt64(Value));
                break;
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                obj[nameof(Value)] = new JValue(Value.ToString());
                break;
            case RegistryValueKind.MultiString:
                if (Value is string[] sa)
                    obj[nameof(Value)] = new JArray(sa);
                else
                    obj[nameof(Value)] = JToken.FromObject(Value);
                break;
            case RegistryValueKind.Binary:
                if (Value is byte[] bytes)
                    obj[nameof(Value)] = Convert.ToBase64String(bytes);
                else
                    obj[nameof(Value)] = Value.ToString();
                break;
            default:
                // Fallback: serialize as token (strings/numbers/arrays)
                obj[nameof(Value)] = JToken.FromObject(Value);
                break;
        }

        return obj;
    }

    /// <summary>
    ///     Deserializes a <see cref="RegistryRevertStep" /> from JSON data.
    /// </summary>
    /// <param name="data">The JSON data to deserialize.</param>
    /// <returns>A new <see cref="RegistryRevertStep" /> instance.</returns>
    public static RegistryRevertStep FromData(JObject data)
    {
        var kind = Enum.Parse<RegistryValueKind>(data[nameof(Kind)]!.ToString());
        var token = data[nameof(Value)];

        object? value = null;
        if (token != null && token.Type != JTokenType.Null)
            switch (kind)
            {
                case RegistryValueKind.DWord:
                    value = token.ToObject<int>();
                    break;
                case RegistryValueKind.QWord:
                    value = token.ToObject<long>();
                    break;
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    value = token.ToObject<string>();
                    break;
                case RegistryValueKind.MultiString:
                    value = token.ToObject<string[]>();
                    break;
                case RegistryValueKind.Binary:
                    // stored as base64 string
                    var base64 = token.ToObject<string>();
                    value = base64 != null ? Convert.FromBase64String(base64) : null;
                    break;
                default:
                    // best-effort
                    value = token.ToObject<object>();
                    break;
            }

        // Load created subkeys list
        List<string>? createdSubKeys = null;
        if (data[nameof(CreatedSubKeys)] is JArray subKeysArray) createdSubKeys = subKeysArray.ToObject<List<string>>();

        List<RegistryRevertStep>? subSteps = null;
        if (data[nameof(SubSteps)] is JArray stepsArray)
        {
            subSteps = [];
            foreach (var stepToken in stepsArray)
                if (stepToken is JObject stepObj)
                    subSteps.Add(FromData(stepObj));
        }

        return new RegistryRevertStep
        {
            Action = Enum.Parse<RevertAction>(data[nameof(Action)]!.ToString()),
            Path = data[nameof(Path)]!.ToString(),
            Name = data[nameof(Name)]?.ToObject<string>(),
            Value = value,
            Kind = kind,
            CreatedSubKeys = createdSubKeys,
            SubSteps = subSteps
        };
    }

    private bool ExecuteSubSteps()
    {
        if (SubSteps == null) return true;
        foreach (var step in SubSteps)
        {
            // Run synchronously to ensure correct nested tree creation order
            if (!step.ExecuteAsync().GetAwaiter().GetResult()) return false;
        }
        return true;
    }
}

/// <summary>
///     Specifies the type of revert action to perform on a registry value.
/// </summary>
public enum RevertAction
{
    /// <summary>The value did not exist before; delete it during revert.</summary>
    NoPreviousValue,

    /// <summary>Restore the previously captured value.</summary>
    RestorePrevious,

    /// <summary>The key did not exist before; delete tree during revert.</summary>
    DeleteKey,

    /// <summary>The key existed before; recreate it during revert (limited to root).</summary>
    RestoreKey,

    /// <summary>The key tree existed before; recreate the entire tree including values sequentially.</summary>
    RestoreKeyTree
}