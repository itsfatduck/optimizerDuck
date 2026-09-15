using optimizerDuck.Domain.Optimizations.Models.Services;

namespace optimizerDuck.Domain.Execution;

/// <summary>
///     The structured facts of one recorded step, as the kind of operation it is. Each record
///     carries only the fields that operation has, and the UI words a row from the kind it holds.
/// </summary>
public abstract record ChangeDetail
{
    /// <summary>
    ///     Stable operation name, for example <c>registry.write</c>. The UI localizes it.
    /// </summary>
    public abstract string Operation { get; }

    /// <summary>
    ///     Why a step wrote nothing, as a stable code the UI words itself, for example
    ///     <c>service.notFound</c>. Null when there is nothing to explain. The one fact every kind
    ///     may carry, because "why nothing was written" does not depend on what the operation is.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///     Whether this step moved between two values. A revert puts the pair back the other way
    ///     round for those steps, and leaves a step that only counts things, like the USB power
    ///     pass, exactly as it is. True is derived from the kind of operation, never passed in.
    /// </summary>
    public virtual bool HasValuePair => false;
}

/// <summary>Wrote a value inside a registry key, replacing whatever was there.</summary>
public sealed record RegistryValueWriteDetail : ChangeDetail
{
    public override string Operation => "registry.write";

    /// <summary>The key the value lives in.</summary>
    public required string Target { get; init; }

    /// <summary>The name of the value inside the key. Null for a key's default value.</summary>
    public string? ValueName { get; init; }

    /// <summary>The kind of data written, for example a registry value kind.</summary>
    public string? ValueType { get; init; }

    /// <summary>The value before the step. Null means the value was not there at all.</summary>
    public string? PreviousValue { get; init; }

    /// <summary>The value after the step.</summary>
    public string? NewValue { get; init; }

    public override bool HasValuePair => true;
}

/// <summary>Removed a value from a registry key.</summary>
public sealed record RegistryValueRemoveDetail : ChangeDetail
{
    public override string Operation => "registry.delete";

    /// <summary>The key the value lived in.</summary>
    public required string Target { get; init; }

    /// <summary>The name of the value that was removed. Null for a key's default value.</summary>
    public string? ValueName { get; init; }

    /// <summary>The kind of data removed, for example a registry value kind.</summary>
    public string? ValueType { get; init; }

    /// <summary>What the value held before removal. Null if it held nothing readable.</summary>
    public string? PreviousValue { get; init; }

    public override bool HasValuePair => true;
}

/// <summary>Created a registry key.</summary>
public sealed record RegistryKeyCreateDetail : ChangeDetail
{
    public override string Operation => "registry.createKey";

    /// <summary>The key that was created.</summary>
    public required string Target { get; init; }
}

/// <summary>Deleted a registry key tree.</summary>
public sealed record RegistryKeyRemoveDetail : ChangeDetail
{
    public override string Operation => "registry.deleteKey";

    /// <summary>The key tree that was deleted.</summary>
    public required string Target { get; init; }
}

/// <summary>Changed how a Windows service starts.</summary>
public sealed record ServiceStartupDetail : ChangeDetail
{
    public override string Operation => "service.startup";

    /// <summary>The name of the service.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The startup type before the step, when it could be read.</summary>
    public ServiceStartupType? PreviousStartupType { get; init; }

    /// <summary>The startup type after the step, when it was set.</summary>
    public ServiceStartupType? NewStartupType { get; init; }

    public override bool HasValuePair => true;
}

/// <summary>Enabled a scheduled task.</summary>
public sealed record ScheduledTaskEnableDetail : ChangeDetail
{
    public override string Operation => "task.enable";

    /// <summary>The full path of the task.</summary>
    public required string TaskPath { get; init; }

    /// <summary>Whether the task was enabled before the step. Null when unread.</summary>
    public bool? PreviousEnabled { get; init; }

    /// <summary>Whether the task is enabled after the step. Null when it wrote nothing.</summary>
    public bool? NewEnabled { get; init; }

    public override bool HasValuePair => true;
}

/// <summary>Disabled a scheduled task.</summary>
public sealed record ScheduledTaskDisableDetail : ChangeDetail
{
    public override string Operation => "task.disable";

    /// <summary>The full path of the task.</summary>
    public required string TaskPath { get; init; }

    /// <summary>Whether the task was enabled before the step. Null when unread.</summary>
    public bool? PreviousEnabled { get; init; }

    /// <summary>Whether the task is enabled after the step. Null when it wrote nothing.</summary>
    public bool? NewEnabled { get; init; }

    public override bool HasValuePair => true;
}

/// <summary>Made an existing power plan the active one.</summary>
public sealed record PowerPlanActivateDetail : ChangeDetail
{
    public override string Operation => "power.plan";

    /// <summary>The plan that was activated.</summary>
    public required string PlanName { get; init; }

    /// <summary>The plan that was active before the step, when it could be named.</summary>
    public string? PreviousPlanName { get; init; }

    /// <summary>The plan that is active after the step.</summary>
    public string? NewPlanName { get; init; }

    public override bool HasValuePair => true;
}

/// <summary>Installed a power plan from a file and activated it.</summary>
public sealed record PowerPlanInstallDetail : ChangeDetail
{
    public override string Operation => "power.planInstall";

    /// <summary>The plan that was installed.</summary>
    public required string PlanName { get; init; }
}

/// <summary>Changed the mains and battery values of one power setting.</summary>
public sealed record PowerSettingDetail : ChangeDetail
{
    public override string Operation => "power.setting";

    /// <summary>The setting's identifier, which is what a revert needs.</summary>
    public required string SettingId { get; init; }

    /// <summary>The setting's name as Windows reports it, when Windows can name it.</summary>
    public string? SettingName { get; init; }

    /// <summary>The values before the step, as one line.</summary>
    public string? PreviousValue { get; init; }

    /// <summary>The values after the step, as one line.</summary>
    public string? NewValue { get; init; }

    public override bool HasValuePair => true;
}

/// <summary>Turned hibernation and Fast Startup off.</summary>
public sealed record HibernationDetail : ChangeDetail
{
    public override string Operation => "hibernation";

    /// <summary>
    ///     Whether the hibernation file was there before the step. Null means the state could not
    ///     be read, which is its own answer: a revert then leaves the system alone rather than
    ///     guessing, and the row says the state is unknown.
    /// </summary>
    public bool? PreviousPresent { get; init; }

    /// <summary>Whether the hibernation file is there after the step.</summary>
    public bool? NewPresent { get; init; }

    public override bool HasValuePair => true;
}

/// <summary>Turned USB power saving off across the machine's root hubs.</summary>
public sealed record UsbPowerDetail : ChangeDetail
{
    public override string Operation => "usb.power";

    /// <summary>How many devices the pass touched, or found already at the target.</summary>
    public int DeviceCount { get; init; }
}
