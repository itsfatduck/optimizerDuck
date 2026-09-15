using optimizerDuck.Domain.Abstractions;

namespace optimizerDuck.Domain.Execution;

/// <summary>
///     The result of a single provider operation. Errors travel in the result, not ambient state.
/// </summary>
public sealed record OpResult(
    bool Ok,
    IRevertStep? Revert = null,
    string? Error = null,
    string? ErrorDetail = null
)
{
    /// <summary>Creates a result that reports the operation succeeded.</summary>
    /// <param name="revert">The compensation step that undoes the change, when one exists.</param>
    /// <returns>A result that reports success and carries the compensation step.</returns>
    public static OpResult Success(IRevertStep? revert = null)
    {
        return new OpResult(true, revert);
    }

    /// <summary>Creates a result that reports the operation failed.</summary>
    /// <param name="error">The message that explains the failure.</param>
    /// <param name="errorDetail">Diagnostic detail for the failure, when one was captured.</param>
    /// <returns>A result that reports failure with the given messages.</returns>
    public static OpResult Fail(string error, string? errorDetail = null)
    {
        return new OpResult(false, null, error, errorDetail);
    }

    /// <summary>
    ///     Aggregates a batch of results: the first failure wins, otherwise success.
    ///     Callers must materialise <paramref name="results"/> first. A lazy sequence
    ///     would stop at the first failure and skip the remaining operations.
    /// </summary>
    public static OpResult FirstFailure(IEnumerable<OpResult> results)
    {
        foreach (var result in results)
            if (!result.Ok)
                return result;

        return Success();
    }
}
