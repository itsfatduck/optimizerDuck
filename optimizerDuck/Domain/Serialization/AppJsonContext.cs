using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Bloatware;
using optimizerDuck.Domain.Revert;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RevertData))]
[JsonSerializable(typeof(RevertStepData))]
[JsonSerializable(typeof(ChangeRecord))]
[JsonSerializable(typeof(ChangeRecordStep))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
[JsonSerializable(typeof(AppXPackage))]
[JsonSerializable(typeof(List<AppXPackage>))]
[JsonSerializable(typeof(List<UsbPowerRevertStep.DeviceState>))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<string>))]
internal partial class AppJsonContext : JsonSerializerContext
{
    public static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };
}

internal static class JsonNodeHelpers
{
    public static string? AsString(JsonNode? node)
    {
        if (node is null)
            return null;

        if (node is JsonValue val)
        {
            if (val.TryGetValue<string>(out var s))
                return s;
            if (val.TryGetValue<JsonElement>(out var el))
                return el.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => el.GetString(),
                    _ => el.ToString(),
                };
            return val.ToString();
        }

        return node.ToString();
    }

    public static bool TryGetBool(JsonNode? node, out bool result)
    {
        if (node is JsonValue val)
        {
            if (val.TryGetValue<bool>(out result))
                return true;
            if (val.TryGetValue<string>(out var s) && bool.TryParse(s, out result))
                return true;
            if (val.TryGetValue<JsonElement>(out var el))
            {
                if (el.ValueKind == JsonValueKind.True)
                {
                    result = true;
                    return true;
                }
                if (el.ValueKind == JsonValueKind.False)
                {
                    result = false;
                    return true;
                }
                if (
                    el.ValueKind == JsonValueKind.String
                    && bool.TryParse(el.GetString(), out result)
                )
                    return true;
            }
        }

        result = false;
        return false;
    }

    public static int GetInt32(JsonNode? node, int defaultValue = 0)
    {
        if (node is JsonValue val)
        {
            if (val.TryGetValue<int>(out var i))
                return i;
            if (val.TryGetValue<long>(out var l))
                return checked((int)l);
            if (val.TryGetValue<string>(out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            if (val.TryGetValue<JsonElement>(out var el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var ei))
                    return ei;
                if (
                    el.ValueKind == JsonValueKind.String
                    && int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ep)
                )
                    return ep;
            }
        }

        return defaultValue;
    }

    public static long GetInt64(JsonNode? node, long defaultValue = 0)
    {
        if (node is JsonValue val)
        {
            if (val.TryGetValue<long>(out var l))
                return l;
            if (val.TryGetValue<int>(out var i))
                return i;
            if (val.TryGetValue<string>(out var s) && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            if (val.TryGetValue<JsonElement>(out var el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var el64))
                    return el64;
                if (
                    el.ValueKind == JsonValueKind.String
                    && long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ep)
                )
                    return ep;
            }
        }

        return defaultValue;
    }

    public static bool TryGetUInt32(JsonNode? node, out uint result)
    {
        if (node is JsonValue val)
        {
            if (val.TryGetValue<uint>(out result))
                return true;
            if (val.TryGetValue<long>(out var l) && l is >= 0 and <= uint.MaxValue)
            {
                result = (uint)l;
                return true;
            }
            if (
                val.TryGetValue<string>(out var s)
                && uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
            )
                return true;
            if (val.TryGetValue<JsonElement>(out var el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetUInt32(out result))
                    return true;
                if (
                    el.ValueKind == JsonValueKind.String
                    && uint.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                )
                    return true;
            }
        }

        result = 0;
        return false;
    }
}
