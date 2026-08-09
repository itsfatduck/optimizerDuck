using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Base condition that requires the CPU to belong to a specific brand.</summary>
public abstract class CpuBrandCondition : ConditionBase
{
    /// <summary>The required CPU vendor.</summary>
    protected abstract CpuVendor RequiredVendor { get; }

    /// <summary>Strongly-typed localized failure title.</summary>
    protected abstract Func<string> Title { get; }

    /// <summary>Strongly-typed localized failure description.</summary>
    protected abstract Func<string> Description { get; }

    public override ConditionResult Evaluate(SystemSnapshot snapshot)
    {
        // An unknown CPU vendor means hardware detection produced no usable answer
        // (e.g. WMI/registry failed), fail open so a detection failure never hides
        // every vendor-specific tweak.
        if (snapshot.Cpu.Vendor == CpuVendor.Unknown)
            return ConditionResult.Error();

        return snapshot.Cpu.Vendor == RequiredVendor
            ? ConditionResult.Available
            : ConditionResult.Unsupported(Title, Description);
    }
}

/// <summary>Requires an Intel CPU.</summary>
public sealed class IntelCpuCondition : CpuBrandCondition
{
    protected override CpuVendor RequiredVendor => CpuVendor.Intel;
    protected override Func<string> Title => () => Translations.Condition_Cpu_Intel_Title;
    protected override Func<string> Description => () => Translations.Condition_Cpu_Intel_Description;
}

/// <summary>Requires an AMD CPU.</summary>
public sealed class AmdCpuCondition : CpuBrandCondition
{
    protected override CpuVendor RequiredVendor => CpuVendor.AMD;
    protected override Func<string> Title => () => Translations.Condition_Cpu_Amd_Title;
    protected override Func<string> Description => () => Translations.Condition_Cpu_Amd_Description;
}
