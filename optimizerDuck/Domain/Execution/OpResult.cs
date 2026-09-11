using optimizerDuck.Domain.Abstractions;

namespace optimizerDuck.Domain.Execution;

/// <summary>
///     The result of a single provider operation. Errors flow here
///     instead of ambient error state.
/// </summary>
public sealed record OpResult(
    bool Ok,
    IRevertStep? Revert = null,
    string? Error = null,
    string? ErrorDetail = null
)
{
    public static OpResult Success(IRevertStep? revert = null)
    {
        return new OpResult(true, revert);
    }

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
