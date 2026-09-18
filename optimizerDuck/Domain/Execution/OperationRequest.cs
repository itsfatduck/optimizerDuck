using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;

namespace optimizerDuck.Domain.Execution;

/// <summary>
///     One run: the subject it belongs to, the work to invoke, and whether its compensation is
///     persisted.
/// </summary>
/// <param name="Subject">The owner the run's record and revert data belong to.</param>
/// <param name="DisplayName">The name user-facing messages carry.</param>
/// <param name="RunLogger">The logger the run writes through.</param>
/// <param name="Revert">Whether the run persists revert data.</param>
/// <param name="Work">The work to invoke once with the run's context.</param>
public sealed record OperationRequest(
    OperationSubject Subject,
    string DisplayName,
    ILogger RunLogger,
    RevertPersistence Revert,
    Func<IProgress<ProcessingProgress>, OptimizationContext, Task<ApplyResult>> Work
);
