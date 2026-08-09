namespace optimizerDuck.Domain.Conditions;

/// <summary>
///     Validates condition metadata at discovery time so misconfigured attributes
///     fail fast at startup instead of surfacing as runtime errors later.
/// </summary>
public static class ConditionValidation
{
    /// <summary>
    ///     Ensures <paramref name="conditionType"/> either is <c>null</c> or is a concrete
    ///     <see cref="ICondition"/> with a public parameterless constructor.
    /// </summary>
    /// <param name="conditionType">The condition type declared in an attribute, or <c>null</c>.</param>
    /// <param name="ownerName">The owning item's display name, used in the error message.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the type does not implement <see cref="ICondition"/> or has no
    ///     parameterless constructor.
    /// </exception>
    public static void Validate(Type? conditionType, string ownerName)
    {
        if (conditionType is null)
            return;

        if (!typeof(ICondition).IsAssignableFrom(conditionType))
            throw new InvalidOperationException(
                $"{ownerName}: condition type {conditionType.FullName} does not implement ICondition."
            );

        if (conditionType.IsAbstract)
            throw new InvalidOperationException(
                $"{ownerName}: condition type {conditionType.FullName} must be a concrete type."
            );

        if (conditionType.GetConstructor(Type.EmptyTypes) is null)
            throw new InvalidOperationException(
                $"{ownerName}: condition type {conditionType.FullName} must have a public parameterless constructor."
            );
    }
}
