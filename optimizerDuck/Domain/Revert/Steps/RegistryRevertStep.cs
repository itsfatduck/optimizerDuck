using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.Serialization;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Revert.Steps;

/// <summary>
///     Represents a revert step that restores or deletes a registry value.
/// </summary>
public class RegistryRevertStep : IRevertStep
{
    /// <summary>
    ///     Gets the action to perform (e.g., restore previous value, delete value,
    ///     restore key tree).
    /// </summary>
    public RevertAction Action { get; init; }

    /// <summary>
    ///     Gets the registry key path, for example
    ///     <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer</c>.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    ///     Gets the registry value name, or <see langword="null" /> for the default value.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Gets the list of subkeys that were created during the apply operation.
    ///     Ordered deepest first for correct cleanup order.
    /// </summary>
    public IReadOnlyList<string>? CreatedSubKeys { get; init; }

    /// <summary>
    ///     Gets nested sub-steps to perform a complex action like restoring a full registry tree.
    /// </summary>
    public IReadOnlyList<RegistryRevertStep>? SubSteps { get; init; }

    /// <summary>
    ///     Gets the original registry value to restore, or <see langword="null" /> if
    ///     the value did not exist.
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    ///     Gets the registry value kind (DWord, QWord, String, Binary, MultiString, ExpandString).
    /// </summary>
    public RegistryValueKind Kind { get; init; }

    /// <inheritdoc />
    public string Type => "Registry";

    /// <inheritdoc />
    public string Description =>
        Action switch
        {
            RevertAction.RestorePrevious => Loc.Instance[
                "Revert.Registry.Description.Restore",
                Path,
                Name ?? "(Default)"
            ],
            RevertAction.NoPreviousValue => Loc.Instance[
                "Revert.Registry.Description.Delete",
                Path,
                Name ?? "(Default)"
            ],
            RevertAction.RestoreKey => Loc.Instance["Revert.Registry.Description.RestoreKey", Path],
            RevertAction.DeleteKey => Loc.Instance["Revert.Registry.Description.DeleteKey", Path],
            RevertAction.RestoreKeyTree => Loc.Instance[
                "Revert.Registry.Description.RestoreKey",
                Path
            ],
            _ => Loc.Instance["Revert.Registry.Description.RevertUnknown", Path],
        };

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(RevertContext context, ILogger logger)
    {
        var opCall = new OpCall { Logger = logger };
        OpResult result = Action switch
        {
            RevertAction.NoPreviousValue => RegistryService.DeleteValue(
                opCall,
                new RegistryItem(Path, Name!)
            ),

            RevertAction.RestorePrevious => Value == null
                ? RegistryService.DeleteValue(opCall, new RegistryItem(Path, Name!))
                : RegistryService.Write(opCall, new RegistryItem(Path, Name!, Value, Kind)),

            RevertAction.RestoreKey => RegistryService.CreateSubKey(opCall, new RegistryItem(Path)),

            RevertAction.DeleteKey => RegistryService.DeleteSubKeyTree(
                opCall,
                new RegistryItem(Path)
            ),

            RevertAction.RestoreKeyTree => await ExecuteSubStepsAsync(context, logger)
                .ConfigureAwait(false)
                ? OpResult.Success()
                : OpResult.Fail(Description),

            _ => OpResult.Fail(Description),
        };

        if (!result.Ok)
            throw new StepExecutionException(result.Error ?? Description, result.ErrorDetail);

        // Read-back verify for the local value actions.
        VerifyRestore(opCall);

        if (CreatedSubKeys?.Count > 0)
            RegistryService.CleanupEmptyKeys(CreatedSubKeys, opCall.Logger);

        return true;
    }

    /// <inheritdoc />
    public JsonObject ToData()
    {
        var obj = new JsonObject
        {
            [nameof(Action)] = Action.ToString(),
            [nameof(Path)] = Path,
            [nameof(Name)] = Name,
            [nameof(Kind)] = Kind.ToString(),
        };

        if (CreatedSubKeys?.Count > 0)
        {
            var subKeys = new JsonArray();
            foreach (var key in CreatedSubKeys)
                subKeys.Add(key);
            obj[nameof(CreatedSubKeys)] = subKeys;
        }

        if (SubSteps?.Count > 0)
        {
            var subArray = new JsonArray();
            foreach (var step in SubSteps)
                subArray.Add(step.ToData());
            obj[nameof(SubSteps)] = subArray;
        }

        // Serialize Value based on Kind to ensure deterministic JSON and avoid
        // rendering ambiguities.
        if (Value == null)
        {
            obj[nameof(Value)] = null;
            return obj;
        }

        switch (Kind)
        {
            case RegistryValueKind.DWord:
                obj[nameof(Value)] = Convert.ToInt32(Value);
                break;
            case RegistryValueKind.QWord:
                obj[nameof(Value)] = Convert.ToInt64(Value);
                break;
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                obj[nameof(Value)] = Value.ToString();
                break;
            case RegistryValueKind.MultiString:
                if (Value is string[] sa)
                {
                    var arr = new JsonArray();
                    foreach (var s in sa)
                        arr.Add(s);
                    obj[nameof(Value)] = arr;
                }
                else
                {
                    obj[nameof(Value)] = Value.ToString();
                }
                break;
            case RegistryValueKind.Binary:
                if (Value is byte[] bytes)
                    obj[nameof(Value)] = Convert.ToBase64String(bytes);
                else
                    obj[nameof(Value)] = Value.ToString();
                break;
            default:
                obj[nameof(Value)] = Value switch
                {
                    int i => JsonValue.Create(i),
                    long l => JsonValue.Create(l),
                    bool b => JsonValue.Create(b),
                    string[] arr => ToJsonArray(arr),
                    _ => JsonValue.Create(Value.ToString()),
                };
                break;
        }

        return obj;
    }

    private static JsonArray ToJsonArray(string[] values)
    {
        var arr = new JsonArray();
        foreach (var item in values)
            arr.Add(item);
        return arr;
    }

    /// <summary>
    ///     Deserializes a <see cref="RegistryRevertStep" /> from JSON data.
    /// </summary>
    /// <param name="data">The JSON data to deserialize.</param>
    /// <returns>A new <see cref="RegistryRevertStep" /> instance.</returns>
    public static RegistryRevertStep FromData(JsonObject data)
    {
        var kind = Enum.Parse<RegistryValueKind>(
            JsonNodeHelpers.AsString(data[nameof(Kind)])!
        );
        var token = data[nameof(Value)];

        object? value = null;
        if (
            token is not null
            && !(token is JsonValue jv && jv.GetValueKind() == JsonValueKind.Null)
        )
        {
            switch (kind)
            {
                case RegistryValueKind.DWord:
                    value = JsonNodeHelpers.GetInt32(token);
                    break;
                case RegistryValueKind.QWord:
                    value = JsonNodeHelpers.GetInt64(token);
                    break;
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    value = JsonNodeHelpers.AsString(token);
                    break;
                case RegistryValueKind.MultiString:
                    if (token is JsonArray arr)
                    {
                        var items = new string[arr.Count];
                        for (var i = 0; i < arr.Count; i++)
                            items[i] = JsonNodeHelpers.AsString(arr[i]) ?? string.Empty;
                        value = items;
                    }
                    break;
                case RegistryValueKind.Binary:
                    // stored as base64 string
                    var base64 = JsonNodeHelpers.AsString(token);
                    value = base64 != null ? Convert.FromBase64String(base64) : null;
                    break;
                default:
                    value = JsonNodeHelpers.AsString(token);
                    break;
            }
        }

        List<string>? createdSubKeys = null;
        if (data[nameof(CreatedSubKeys)] is JsonArray subKeysArray)
        {
            createdSubKeys = new List<string>(subKeysArray.Count);
            foreach (var item in subKeysArray)
                if (JsonNodeHelpers.AsString(item) is { } key)
                    createdSubKeys.Add(key);
        }

        List<RegistryRevertStep>? subSteps = null;
        if (data[nameof(SubSteps)] is JsonArray stepsArray)
        {
            subSteps = new List<RegistryRevertStep>(stepsArray.Count);
            foreach (var stepToken in stepsArray)
                if (stepToken is JsonObject stepObj)
                    subSteps.Add(FromData(stepObj));
        }

        return new RegistryRevertStep
        {
            Action = Enum.Parse<RevertAction>(JsonNodeHelpers.AsString(data[nameof(Action)])!),
            Path = JsonNodeHelpers.AsString(data[nameof(Path)])!,
            Name = JsonNodeHelpers.AsString(data[nameof(Name)]),
            Value = value,
            Kind = kind,
            CreatedSubKeys = createdSubKeys,
            SubSteps = subSteps,
        };
    }

    private void VerifyRestore(OpCall call)
    {
        switch (Action)
        {
            case RevertAction.RestorePrevious when Value != null:
            {
                var item = new RegistryItem(Path, Name!);
                if (!RegistryService.TryReadValue(item, out var actual, call.Logger))
                    throw new StepExecutionException(
                        $"Registry verify failed at {Path}:{Name}: could not read the value.",
                        null
                    );
                if (!ValuesEqual(actual, Value, Kind))
                    throw new StepExecutionException(
                        $"Registry verify failed at {Path}:{Name}: expected '{Value}', actual '{actual}'",
                        null
                    );
                break;
            }
            case RevertAction.NoPreviousValue:
            case RevertAction.RestorePrevious:
            {
                // a failed read is not "absent".
                if (
                    !RegistryService.TryReadValue(
                        new RegistryItem(Path, Name!),
                        out var actual,
                        call.Logger
                    )
                )
                    throw new StepExecutionException(
                        $"Registry verify failed at {Path}:{Name}: could not read the value.",
                        null
                    );
                if (actual != null)
                    throw new StepExecutionException(
                        $"Registry verify failed at {Path}:{Name}: expected '<absent>', actual '{actual}'",
                        null
                    );
                break;
            }
            case RevertAction.RestoreKey:
                if (
                    !RegistryService.TryKeyExists(
                        new RegistryItem(Path),
                        out var restoreExists,
                        call.Logger
                    )
                )
                    throw new StepExecutionException(
                        $"Registry verify failed at {Path}: could not query the key.",
                        null
                    );
                if (!restoreExists)
                    throw new StepExecutionException(
                        $"Registry verify failed at {Path}: expected key to exist, but it was missing",
                        null
                    );
                break;
            case RevertAction.DeleteKey:
                if (
                    !RegistryService.TryKeyExists(
                        new RegistryItem(Path),
                        out var deleteExists,
                        call.Logger
                    )
                )
                    throw new StepExecutionException(
                        $"Registry verify failed at {Path}: could not query the key.",
                        null
                    );
                if (deleteExists)
                    throw new StepExecutionException(
                        $"Registry verify failed at {Path}: expected key to be absent, but it still exists",
                        null
                    );
                break;
            default:
                break;
        }
    }

    private static bool ValuesEqual(object? actual, object? expected, RegistryValueKind kind) =>
        RegistryValues.Equal(actual, expected, kind);

    private async Task<bool> ExecuteSubStepsAsync(RevertContext context, ILogger logger)
    {
        if (SubSteps == null)
            return true;
        foreach (var step in SubSteps)
        {
            if (!await step.ExecuteAsync(context, logger))
                return false;
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

    /// <summary>
    ///     The key tree existed before; recreate the entire tree including values
    ///     sequentially.
    /// </summary>
    RestoreKeyTree,
}
