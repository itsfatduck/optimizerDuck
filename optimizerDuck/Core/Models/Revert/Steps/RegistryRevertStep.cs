using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.OptimizationServices;

namespace optimizerDuck.Core.Models.Revert.Steps;

public class RegistryRevertStep : IRevertStep
{
    public RevertAction Action { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? Name { get; init; }

    /// <summary>
    ///     List of subkeys that were created during the apply operation (in order of creation, deepest first for cleanup)
    /// </summary>
    public IReadOnlyList<string>? CreatedSubKeys { get; init; }

    public object? Value { get; init; }
    public RegistryValueKind Kind { get; init; }
    public string Type => "Registry";

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

            // Cleanup empty subkeys if they were created during apply
            if (result && CreatedSubKeys?.Count > 0) RegistryService.CleanupEmptyKeys(CreatedSubKeys);

            return result;
        });
    }


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
                    value = token.Type == JTokenType.Array ? token.ToObject<string[]>() : token.ToObject<string[]>();
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

public enum RevertAction
{
    NoPreviousValue,
    RestorePrevious
}