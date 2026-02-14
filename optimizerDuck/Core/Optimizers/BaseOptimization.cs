using System.Reflection;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;
using Wpf.Ui.Controls;
using OptimizationState = optimizerDuck.Core.Models.UI.OptimizationState;

namespace optimizerDuck.Core.Optimizers;

public abstract class BaseOptimization : IOptimization
{
    private OptimizationAttribute? _meta;

    private OptimizationAttribute Meta =>
        _meta ??= GetType().GetCustomAttribute<OptimizationAttribute>()
                  ?? throw new InvalidOperationException(
                      $"{GetType().Name} is missing [Optimization] attribute");

    public Type? OwnerType { get; set; }

    public string OwnerKey =>
        OwnerType?.Name
        ?? throw new InvalidOperationException(
            $"{GetType().Name} has no owner assigned");

    public OptimizationRiskVisual RiskVisual => Risk switch
    {
        OptimizationRisk.Safe => new OptimizationRiskVisual
        {
            Display = Translations.Optimizer_UI_Risk_Safe,
            Icon = SymbolRegular.ShieldCheckmark24
        },
        OptimizationRisk.Moderate => new OptimizationRiskVisual
        {
            Display = Translations.Optimizer_UI_Risk_Moderate,
            Icon = SymbolRegular.Warning24
        },
        OptimizationRisk.Risky => new OptimizationRiskVisual
        {
            Display = Translations.Optimizer_UI_Risk_Risky,
            Icon = SymbolRegular.ShieldError24
        },
        _ => new OptimizationRiskVisual
        {
            Display = Translations.Optimizer_UI_Risk_Safe,
            Icon = SymbolRegular.ShieldCheckmark24
        }
    };

    public IEnumerable<OptimizationTagDisplay> TagDisplays =>
        Meta.Tags.ToDisplays();

    public string Prefix => Loc.Instance[$"Optimizer.{OwnerKey}.{OptimizationKey}"];
    public string ProgressPrefix => Loc.Instance[$"{Prefix}.Progress"];
    public string ErrorPrefix => Loc.Instance[$"{Prefix}.Error"];

    public Guid Id => Guid.Parse(Meta.Id);
    public OptimizationRisk Risk => Meta.Risk;

    public string OptimizationKey => GetType().Name;

    public OptimizationState State { get; set; } = new();

    public string Name => Loc.Instance[$"{Prefix}.Name"];
    public string ShortDescription => Loc.Instance[$"{Prefix}.ShortDescription"];

    public abstract Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context);
}

/*
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.Views.Pages.Optimizations;

namespace optimizerDuck.Core.Optimizers;

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