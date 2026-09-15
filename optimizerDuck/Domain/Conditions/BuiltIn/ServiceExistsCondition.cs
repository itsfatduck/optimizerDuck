using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Base condition that requires a Windows service to exist on the machine.
///     Subclasses supply the service name (the registry subkey under
///     <c>HKLM\SYSTEM\CurrentControlSet\Services</c>).
/// </summary>
public abstract class ServiceExistsCondition : ConditionBase
{
    protected abstract string ServiceName { get; }

    /// <summary>Supplies the localized failure title.</summary>
    protected abstract Func<string> Title { get; }

    /// <summary>Supplies the localized failure description.</summary>
    protected abstract Func<string> Description { get; }

    public override ConditionResult Evaluate(SystemInfo snapshot)
    {
        var serviceKey = new RegistryItem($@"HKLM\SYSTEM\CurrentControlSet\Services\{ServiceName}");
        if (!RegistryService.TryKeyExists(serviceKey, out var exists))
            return ConditionResult.Error();

        return exists ? ConditionResult.Available : ConditionResult.Unsupported(Title, Description);
    }
}
