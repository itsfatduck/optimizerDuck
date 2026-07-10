using System.Reflection;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using Wpf.Ui.Controls;
using OptimizationState = optimizerDuck.Domain.UI.OptimizationState;

namespace optimizerDuck.Domain.Optimizations.Models;


/// <summary>
///     Base class for all optimizations. Subclasses implement <see cref="ApplyAsync"/> and are
///     decorated with <see cref="OptimizationAttribute"/> to provide metadata. The category
///     (<see cref="OwnerType"/>) is assigned automatically during reflection-based discovery.
/// </summary>
public abstract class BaseOptimization : IOptimization
{
    #region Metadata

    private OptimizationAttribute? _meta;

    private OptimizationAttribute Meta =>
        _meta ??=
            GetType().GetCustomAttribute<OptimizationAttribute>()
            ?? throw new InvalidOperationException(
                $"{GetType().Name} is missing [Optimization] attribute"
            );

    /// <summary>
    ///     Gets or sets the type of the category class that owns this optimization.
    ///     Assigned automatically during reflection-based discovery in <c>OptimizationRegistry</c>.
    /// </summary>
    public Type? OwnerType { get; set; }

    /// <summary>Gets the name of the owner category class.</summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="OwnerType"/> has not been assigned.</exception>
    public string OwnerKey =>
        OwnerType?.Name
        ?? throw new InvalidOperationException($"{GetType().Name} has no owner assigned");

    #endregion

    #region Identification

    /// <summary>Gets the unique identifier for this optimization, parsed from the <see cref="OptimizationAttribute.Id"/>.</summary>
    public Guid Id => Guid.Parse(Meta.Id);

    /// <summary>Gets the risk level associated with this optimization.</summary>
    public OptimizationRisk Risk => Meta.Risk;

    /// <summary>Gets the unique key used for localization and identification.</summary>
    public string OptimizationKey => GetType().Name;

    #endregion

    #region Presentation

    /// <summary>Gets the visual representation (icon and localized text) of the risk level.</summary>
    public RiskVisual RiskVisual =>
        Risk switch
        {
            OptimizationRisk.Safe => new RiskVisual
            {
                Display = Translations.Optimizer_UI_Risk_Safe,
                Icon = SymbolRegular.ShieldCheckmark24,
            },
            OptimizationRisk.Moderate => new RiskVisual
            {
                Display = Translations.Optimizer_UI_Risk_Moderate,
                Icon = SymbolRegular.Warning24,
            },
            OptimizationRisk.Risky => new RiskVisual
            {
                Display = Translations.Optimizer_UI_Risk_Risky,
                Icon = SymbolRegular.ShieldError24,
            },
            _ => new RiskVisual
            {
                Display = Translations.Optimizer_UI_Risk_Safe,
                Icon = SymbolRegular.ShieldCheckmark24,
            },
        };

    /// <summary>Gets the collection of tag displays for the UI, derived from <see cref="OptimizationTags"/>.</summary>
    public IEnumerable<OptimizationTagDisplay> TagDisplays => Meta.Tags.ToDisplays();

    /// <summary>Gets or sets the current applied state and timing information for this optimization.</summary>
    public OptimizationState State { get; set; } = new();

    #endregion

    #region Localization

    /// <summary>Gets the full localization prefix for this optimization.</summary>
    public string Prefix => Loc.Instance[$"Optimizer.{OwnerKey}.{OptimizationKey}"];

    /// <summary>Gets the localization prefix for progress messages.</summary>
    public string ProgressPrefix => Loc.Instance[$"{Prefix}.Progress"];

    /// <summary>Gets the localization prefix for error messages.</summary>
    public string ErrorPrefix => Loc.Instance[$"{Prefix}.Error"];

    /// <summary>Gets the localized display name of the optimization.</summary>
    public string Name => Loc.Instance[$"{Prefix}.Name"];

    /// <summary>Gets the localized short description of what this optimization does.</summary>
    public string ShortDescription => Loc.Instance[$"{Prefix}.ShortDescription"];

    #endregion

    public abstract Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    );

    /// <summary>
    ///     Returns an <see cref="ApplyResult" /> derived from steps recorded in the active <see cref="ExecutionScope" />.
    /// </summary>
    protected static ApplyResult CompleteFromScope()
    {
        return ExecutionScope.Current?.ToApplyResult()
            ?? ApplyResult.False(Translations.Revert_Error_NoSteps);
    }
}

/*
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.Pages.Optimizations;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(PAGE))]
public class OPTIMIZER : IOptimizationCategory
{
    public string Name { get; init; } = Loc.Instance[$"Optimizer.{nameof(OPTIMIZER)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.OPTIMIZER_ORDER;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(Id = "OPTIMIZATION_ID", Risk = OptimizationRisk.RISK,
        Tags = OptimizationTags.TAG)]
    public class OPTIMIZATION_NAME : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            // ... Optimization logic here ...
            return Task.FromResult(ApplyResult.True());
        }
    }
}
 */
