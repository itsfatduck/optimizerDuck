using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Base condition that requires at least one GPU from a specific vendor.</summary>
public abstract class GpuBrandCondition : ConditionBase
{
    /// <summary>The required GPU vendor.</summary>
    protected abstract GpuVendor RequiredVendor { get; }

    /// <summary>Strongly-typed localized failure title.</summary>
    protected abstract Func<string> Title { get; }

    /// <summary>Strongly-typed localized failure description.</summary>
    protected abstract Func<string> Description { get; }

    public override ConditionResult Evaluate(SystemSnapshot snapshot)
    {
        // An empty or all-unknown GPU list means hardware detection produced no usable
        // answer (e.g. DXGI/WMI failed), fail open so a detection failure never hides
        // every vendor-specific tweak. "Genuinely no GPU" is indistinguishable from a
        // detection failure here, so treat both as "could not verify".
        if (
            snapshot.Gpus.Count == 0
            || snapshot.Gpus.All(static g => g.Vendor == GpuVendor.Unknown)
        )
            return ConditionResult.Error();

        return snapshot.Gpus.Any(g => g.Vendor == RequiredVendor)
            ? ConditionResult.Available
            : ConditionResult.Unsupported(Title, Description);
    }
}

/// <summary>Requires an NVIDIA GPU.</summary>
public sealed class NvidiaGpuCondition : GpuBrandCondition
{
    protected override GpuVendor RequiredVendor => GpuVendor.NVIDIA;
    protected override Func<string> Title => () => Translations.Condition_Gpu_Nvidia_Title;
    protected override Func<string> Description =>
        () => Translations.Condition_Gpu_Nvidia_Description;
}

/// <summary>Requires an AMD GPU.</summary>
public sealed class AmdGpuCondition : GpuBrandCondition
{
    protected override GpuVendor RequiredVendor => GpuVendor.AMD;
    protected override Func<string> Title => () => Translations.Condition_Gpu_Amd_Title;
    protected override Func<string> Description => () => Translations.Condition_Gpu_Amd_Description;
}

/// <summary>Requires an Intel GPU.</summary>
public sealed class IntelGpuCondition : GpuBrandCondition
{
    protected override GpuVendor RequiredVendor => GpuVendor.Intel;
    protected override Func<string> Title => () => Translations.Condition_Gpu_Intel_Title;
    protected override Func<string> Description =>
        () => Translations.Condition_Gpu_Intel_Description;
}
