using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
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
public abstract partial class BaseOptimization : LocalizedObject, IOptimization
{
    protected BaseOptimization()
    {
        _state.PropertyChanged += OnStateChanged;
    }

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
                Display = Loc.Instance["Optimizer.UI.Risk.Safe"],
                Icon = SymbolRegular.ShieldCheckmark24,
            },
            OptimizationRisk.Moderate => new RiskVisual
            {
                Display = Loc.Instance["Optimizer.UI.Risk.Moderate"],
                Icon = SymbolRegular.Warning24,
            },
            OptimizationRisk.Risky => new RiskVisual
            {
                Display = Loc.Instance["Optimizer.UI.Risk.Risky"],
                Icon = SymbolRegular.ShieldError24,
            },
            _ => new RiskVisual
            {
                Display = Loc.Instance["Optimizer.UI.Risk.Safe"],
                Icon = SymbolRegular.ShieldCheckmark24,
            },
        };

    /// <summary>Gets the collection of tag displays for the UI, derived from <see cref="OptimizationTags"/>.</summary>
    public IEnumerable<OptimizationTagDisplay> TagDisplays => Meta.Tags.ToDisplays();

    private OptimizationState _state = new();

    /// <summary>Gets or sets the current applied state and timing information for this optimization.</summary>
    public OptimizationState State
    {
        get => _state;
        set
        {
            if (ReferenceEquals(_state, value))
                return;

            // Re-subscribe so IsConditionBlocked stays in sync even when State is replaced.
            _state.PropertyChanged -= OnStateChanged;
            _state = value;
            _state.PropertyChanged += OnStateChanged;
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsConditionBlocked));
        }
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OptimizationState.IsApplied))
            return;

        // Forward IsApplied mutations as a State change so external subscribers that
        // listen to the optimization (not the nested State) stay in sync even when the
        // State instance is swapped out later.
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsConditionBlocked));
    }

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

    /// <summary>English name for log (always English).</summary>
    public string LogName => Loc.Invariant[$"Optimizer.{OwnerKey}.{OptimizationKey}.Name"];

    /// <summary>English short description for log (always English).</summary>
    public string LogShortDescription =>
        Loc.Invariant[$"Optimizer.{OwnerKey}.{OptimizationKey}.ShortDescription"];

    #endregion

    #region Condition

    /// <summary>
    ///     Gets the compatibility condition type declared in the <see cref="OptimizationAttribute"/>
    ///     (implementing <see cref="ICondition"/>), or <c>null</c> when always available.
    /// </summary>
    public Type? ConditionType => Meta.Condition;

    /// <summary>Gets or sets the evaluated compatibility result.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConditionBlocked))]
    private ConditionResult _conditionResult = ConditionResult.Available;

    /// <summary>
    ///     Gets or sets a value indicating that the user chose to hide the unsupported
    ///     state for this session and show the normal card instead. Not persisted.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConditionBlocked))]
    private bool _isConditionHidden;

    /// <summary>
    ///     Gets whether this optimization should be presented in the unsupported state.
    ///     The block only applies on first open when the item is <em>not</em> applied;
    ///     once applied (or hidden by the user) the normal card is shown.
    /// </summary>
    public bool IsConditionBlocked =>
        ConditionResult.IsBlocking && !State.IsApplied && !IsConditionHidden;

    #endregion

    /// <inheritdoc />
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
            ?? ApplyResult.False(Loc.Instance["Revert.Error.NoSteps"]);
    }
}
