using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Declarative base for optimizations defined by static registry writes, registry value
///     deletions, services, and scheduled tasks. Automatically detects Group Policy paths, probes
///     current state across all declared targets, and applies every change in order.
/// </summary>
public abstract class DeclarativeOptimization : BaseOptimization
{
    /// <summary>Registry entries written and probed by this optimization.</summary>
    protected virtual RegistryItem[] RegistryItems => [];

    /// <summary>Registry values deleted on apply and verified absent when probing.</summary>
    protected virtual RegistryItem[] DeleteRegistryValues => [];

    /// <summary>Windows services configured on apply and probed via <see cref="StateProbe.ServicesAtLeast" />.</summary>
    protected virtual ServiceItem[] Services => [];

    /// <summary>Scheduled task paths disabled on apply and verified disabled when probing.</summary>
    protected virtual string[] ScheduledTasks => [];

    /// <inheritdoc />
    public override bool SetsGroupPolicy =>
        Metadata.SetsGroupPolicy
        || RegistryItems.Any(static item =>
            item.Path.Contains(@"\Policies\", StringComparison.OrdinalIgnoreCase)
        );

    /// <inheritdoc />
    public override Task<bool?> IsSystemAtTargetAsync(
        SystemInfo snapshot,
        IReadOnlyDictionary<string, string>? appliedOptions = null,
        CancellationToken cancellationToken = default
    )
    {
        var checks = new List<bool?>(4);

        if (RegistryItems.Length > 0)
            checks.Add(StateProbe.RegistryMatches(RegistryItems));

        foreach (var item in DeleteRegistryValues)
        {
            if (item.Name is not null)
                checks.Add(StateProbe.RegistryValueAbsent(item.Path, item.Name));
        }

        if (Services.Length > 0)
            checks.Add(StateProbe.ServicesAtLeast(Services));

        if (ScheduledTasks.Length > 0)
            checks.Add(StateProbe.TasksDisabled(false, ScheduledTasks));

        return Task.FromResult(checks.Count == 0 ? null : StateProbe.All([.. checks]));
    }

    /// <inheritdoc />
    public override async Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    )
    {
        if (RegistryItems.Length > 0)
            RegistryService.Write(context, RegistryItems);

        if (DeleteRegistryValues.Length > 0)
            RegistryService.DeleteValue(context, DeleteRegistryValues);

        if (Services.Length > 0)
            await ServiceProcessService
                .ChangeServiceStartupTypeAsync(context, Services, progress)
                .ConfigureAwait(false);

        foreach (var taskPath in ScheduledTasks)
            ScheduledTaskService.DisableTask(context, taskPath);

        context.Logger.LogInformation("Applied {Optimization}", OptimizationKey);
        return context.Changes.ToApplyResult();
    }
}
