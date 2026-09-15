using optimizerDuck.Domain.Optimizations.Models;

namespace optimizerDuck.Services.Optimization;

/// <summary>
///     Fail-fast discovery validation: a malformed or duplicated optimization ID surfaces at
///     startup with the offending class named, rather than as a
///     <see cref="FormatException" /> when the ID is first read.
/// </summary>
public static class OptimizationValidation
{
    public static void Validate(BaseOptimization optimization, HashSet<Guid> seen)
    {
        var typeName = optimization.GetType().FullName ?? optimization.GetType().Name;
        Guid id;
        try
        {
            id = optimization.Id;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Optimization '{typeName}' has an invalid [Optimization] Id (must be a GUID).",
                ex
            );
        }

        if (id == Guid.Empty)
            throw new InvalidOperationException(
                $"Optimization '{typeName}' has an empty [Optimization] Id."
            );

        if (!seen.Add(id))
            throw new InvalidOperationException(
                $"Duplicate [Optimization] Id {id} (optimization '{typeName}'). IDs must be unique."
            );
    }
}
