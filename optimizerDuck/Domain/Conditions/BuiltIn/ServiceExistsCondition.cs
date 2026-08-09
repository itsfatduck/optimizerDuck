using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.Services.System;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Base condition that requires a Windows service to exist on the machine.
///     Subclasses supply the service name (the registry subkey under
///     <c>HKLM\SYSTEM\CurrentControlSet\Services</c>).
/// </summary>
public abstract class ServiceExistsCondition : ConditionBase
{
    /// <summary>The name of the required service.</summary>
    protected abstract string ServiceName { get; }

    /// <summary>Strongly-typed localized failure title.</summary>
    protected abstract Func<string> Title { get; }

    /// <summary>Strongly-typed localized failure description.</summary>
    protected abstract Func<string> Description { get; }

    public override ConditionResult Evaluate(SystemSnapshot snapshot)
    {
        var serviceKey = new RegistryItem($@"HKLM\SYSTEM\CurrentControlSet\Services\{ServiceName}");
        return RegistryService.KeyExists(serviceKey)
            ? ConditionResult.Available
            : ConditionResult.Unsupported(Title, Description);
    }
}
