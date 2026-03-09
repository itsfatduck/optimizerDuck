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
    public string Description => string.Format(
        Action == RevertAction.RestorePrevious
            ? Translations.Revert_Registry_Description_Restore
            : Translations.Revert_Registry_Description_Delete,
        Path, Name ?? "(Default)");

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync()
    {
        return await Task.Run(() =>
        {
            var valueName = Name ?? string.Empty;

            var result = Action switch
            {
                RevertAction.NoPreviousValue =>
                    RegistryService.DeleteValue(new RegistryItem(Path, valueName)),

                RevertAction.RestorePrevious =>
                    Value == null
                        ? false
                        : RegistryService.Write(new RegistryItem(Path, valueName, Value!, Kind)),

                _ => false
            };

            if (!result)
                throw new Exception(
                    string.Format(Translations
                        .Service_Common_Error_AccessDenied)); // Generic for now, but better than nothing

            // Cleanup empty subkeys if they were created during apply
            if (result && CreatedSubKeys?.Count > 0) RegistryService.CleanupEmptyKeys(CreatedSubKeys);

            return result;
        });
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

        return new RegistryRevertStep
        {
            Action = Enum.Parse<RevertAction>(data[nameof(Action)]!.ToString()),
            Path = data[nameof(Path)]!.ToString(),
            Name = data[nameof(Name)]?.ToObject<string>(),
            Value = value,
            Kind = kind,
            CreatedSubKeys = createdSubKeys
        };
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
    RestorePrevious
}