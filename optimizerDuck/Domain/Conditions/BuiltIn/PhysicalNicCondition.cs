using Microsoft.Win32;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Requires at least one physical network adapter class key in the Windows registry.</summary>
public sealed class PhysicalNicCondition : ConditionBase
{
    internal const string NetworkAdapterClassPath =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    private const int PhysicalAdapterCharacteristic = 0x4;

    /// <summary>Enumerates full HKLM paths of physical network adapter class subkeys.</summary>
    internal static IReadOnlyList<string> GetPhysicalAdapterClassPaths()
    {
        var paths = new List<string>();
        using var classKey = Registry.LocalMachine.OpenSubKey(NetworkAdapterClassPath);
        if (classKey is null)
            return paths;

        foreach (var subKeyName in classKey.GetSubKeyNames())
        {
            if (subKeyName.Length != 4 || !subKeyName.All(char.IsAsciiDigit))
                continue;

            using var adapterKey = classKey.OpenSubKey(subKeyName);
            if (
                adapterKey?.GetValue("Characteristics") is int characteristics
                && (characteristics & PhysicalAdapterCharacteristic) != 0
            )
            {
                paths.Add($@"HKLM\{NetworkAdapterClassPath}\{subKeyName}");
            }
        }

        return paths;
    }

    public override ConditionResult Evaluate(SystemInfo snapshot)
    {
        try
        {
            return GetPhysicalAdapterClassPaths().Count > 0
                ? ConditionResult.Available
                : ConditionResult.Unsupported(
                    () => Loc.Instance["Condition.PhysicalNic.Title"],
                    () => Loc.Instance["Condition.PhysicalNic.Description"]
                );
        }
        catch
        {
            return ConditionResult.Error();
        }
    }
}
