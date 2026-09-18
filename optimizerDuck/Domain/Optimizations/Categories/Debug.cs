#if DEBUG
using System.Collections.ObjectModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.UI.Pages;

namespace optimizerDuck.Domain.Optimizations.Categories;

/// <summary>
///     Provides the base class for the debug optimizations, whose text is hard-coded English.
/// </summary>
public abstract class DebugOptimization : BaseOptimization
{
    protected abstract string EnglishName { get; }

    protected abstract string EnglishDescription { get; }

    public override string Name => EnglishName;

    public override string ShortDescription => EnglishDescription;

    public override string LogName => EnglishName;

    public override string LogShortDescription => EnglishDescription;

    /// <summary>Records a no-op change, so a partial failure reaches the retry dialog.</summary>
    protected static void RecordDebugChange(OptimizationContext context) =>
        context.Changes.Add("Debug step", "wrote a debug value", true, DebugCompensation());

    /// <summary>Creates a compensation that does nothing.</summary>
    protected static ShellRevertStep DebugCompensation() =>
        new() { ShellType = ShellType.CMD, Command = "exit 0" };

    protected const string DebugFailure = "a deliberate debug failure";
}

/// <summary>
///     Provides the debug scenarios reachable by hand from the optimize page. The file is excluded
///     from a release build, so the category is never discovered there.
/// </summary>
/// <remarks>
///     A scenario records steps in the run's own change set and touches nothing on the machine.
/// </remarks>
[OptimizationCategory(typeof(DebugOptimizerPage))]
public class Debug : LocalizedObject, IOptimizationCategory
{
    public string Name => "Debug";

    // Reuses the last production order; a debug category only has to sort somewhere.
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.AI;

    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "95828E58-DAB1-4382-A0B1-F40B6A44156D",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    public class FailedStepThenRetrySucceeds : DebugOptimization
    {
        protected override string EnglishName => "Failed step after a change, retry succeeds";

        protected override string EnglishDescription =>
            "Records a change, then a failed step whose retry succeeds from the dialog.";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RecordDebugChange(context);

            context.Changes.Add(
                "Debug step",
                "the debug step failed, and its retry succeeds",
                false,
                null,
                error: DebugFailure,
                retry: retryCall =>
                {
                    retryCall.Changes.Add(
                        "Debug step",
                        "the retry succeeded",
                        true,
                        DebugCompensation()
                    );
                    return Task.FromResult(OpResult.Success());
                }
            );

            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    [Optimization(
        Id = "DA23B162-A11B-451D-88F1-BE37E250A769",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    public class FailedStepThenRetryFailsAgain : DebugOptimization
    {
        protected override string EnglishName => "Failed step after a change, retry fails again";

        protected override string EnglishDescription =>
            "Records a change, then a failed step whose retry fails again in the dialog.";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RecordDebugChange(context);

            context.Changes.Add(
                "Debug step",
                "the debug step failed, and its retry fails again",
                false,
                null,
                error: DebugFailure,
                retry: retryCall =>
                {
                    retryCall.Changes.Add(
                        "Debug step",
                        "the retry failed again",
                        false,
                        null,
                        error: DebugFailure
                    );
                    return Task.FromResult(OpResult.Fail(DebugFailure));
                }
            );

            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    [Optimization(
        Id = "6A5C5662-61AB-4963-98F6-6C2C85478CB7",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    public class FailedStepWithoutARetry : DebugOptimization
    {
        protected override string EnglishName => "Failed step after a change, no retry";

        protected override string EnglishDescription =>
            "Records a change, then a failed step that carries no retry to offer.";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RecordDebugChange(context);

            context.Changes.Add(
                "Debug step",
                "the debug step failed",
                false,
                null,
                error: DebugFailure
            );

            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    [Optimization(
        Id = "868F8DA4-8D56-44E4-ADFF-4B593164BCB5",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    public class ProviderErrorWithoutAFailedStep : DebugOptimization
    {
        protected override string EnglishName => "Provider reports an error";

        protected override string EnglishDescription =>
            "Reports an error while recording no step, so no failed step is named.";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        ) =>
            Task.FromResult(
                ApplyResult.False("the debug run reported an error and recorded no failed step")
            );
    }

    [Optimization(
        Id = "1C3387DA-2F17-4BE7-B17A-5AAF90794C5D",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    public class ThrowsAfterRecordingAChange : DebugOptimization
    {
        protected override string EnglishName => "Throws after recording a change";

        protected override string EnglishDescription =>
            "Records a change, then throws, so the exception is captured as a failed step.";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RecordDebugChange(context);

            throw new InvalidOperationException("the debug run threw after recording a change");
        }
    }

    [Optimization(
        Id = "78F8BFE3-34EA-47C1-96A3-88DEF36D8A7B",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    public class RecordsAChange : DebugOptimization
    {
        protected override string EnglishName => "Records a change";

        protected override string EnglishDescription =>
            "Records one change and nothing else, so the run is a plain success.";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            RecordDebugChange(context);

            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }

    [Optimization(
        Id = "6B2CAF01-8855-488A-9505-D9123E7BDBC7",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    public class AlreadyInTheDesiredState : DebugOptimization
    {
        protected override string EnglishName => "Already in the desired state";

        protected override string EnglishDescription =>
            "Records one skip, so the run reports that there was nothing to do.";

        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            context.Changes.AddSkip("Debug step", "the debug target already matches");

            return Task.FromResult(context.Changes.ToApplyResult());
        }
    }
}
#endif
