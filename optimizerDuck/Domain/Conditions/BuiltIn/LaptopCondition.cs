using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Requires the device chassis to be identified as a laptop.</summary>
public sealed class LaptopCondition : ConditionBase
{
    public override ConditionResult Evaluate(SystemInfo snapshot)
    {
        if (snapshot.Windows.DeviceKind == DeviceKind.Unknown)
            return ConditionResult.Error();

        return snapshot.Windows.DeviceKind == DeviceKind.Laptop
            ? ConditionResult.Available
            : ConditionResult.Unsupported(
                () => Loc.Instance["Condition.Laptop.Title"],
                () => Loc.Instance["Condition.Laptop.Description"]
            );
    }
}

/// <summary>Requires both Windows 11 and a laptop chassis.</summary>
public sealed class LaptopWindows11Condition : ConditionBase
{
    public override ConditionResult Evaluate(SystemInfo snapshot)
    {
        if (!TryGetOsBuild(snapshot, out var build) || build < WindowsBuilds.Windows11)
            return ConditionResult.Unsupported(
                () => Loc.Instance["Condition.Windows11.Title"],
                () => Loc.Instance["Condition.Windows11.Description"]
            );

        if (snapshot.Windows.DeviceKind == DeviceKind.Unknown)
            return ConditionResult.Error();

        return snapshot.Windows.DeviceKind == DeviceKind.Laptop
            ? ConditionResult.Available
            : ConditionResult.Unsupported(
                () => Loc.Instance["Condition.Laptop.Title"],
                () => Loc.Instance["Condition.Laptop.Description"]
            );
    }
}
