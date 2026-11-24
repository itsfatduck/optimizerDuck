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
using optimizerDuck.Services.System;
using optimizerDuck.Services.System.Primitives;
using Wpf.Ui.Controls;
using OptimizationState = optimizerDuck.Domain.UI.OptimizationState;

namespace optimizerDuck.Domain.Optimizations.Models;

/// <summary>
///     Base class for all optimizations. Subclasses implement <see cref="ApplyAsync" /> and are
///     decorated with <see cref="OptimizationAttribute" /> to provide metadata. The category
///     (<see cref="OwnerType" />) is assigned automatically during reflection-based discovery.
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
    ///     Gets the type of the category class that owns this optimization.
    ///     Assigned automatically during reflection-based discovery in <c>OptimizationRegistry</c>.
    /// </summary>
    public Type? OwnerType { get; internal set; }

    /// <summary>Gets the owner category key.</summary>
    /// <exception cref="InvalidOperationException"><see cref="OwnerType" /> has not been assigned.</exception>
    public string OwnerKey =>
        OwnerType?.Name
        ?? throw new InvalidOperationException($"{GetType().Name} has no owner assigned");

    #endregion

    #region Identification

    private Guid? _id;

    /// <summary>
    ///     Gets the unique identifier declared in <see cref="OptimizationAttribute.Id" />.
    /// </summary>
    public Guid Id => _id ??= Guid.Parse(Meta.Id);

    /// <summary>Gets the risk level associated with this optimization.</summary>
    public OptimizationRisk Risk => Meta.Risk;

    /// <summary>Gets the unique key used for localization and identification.</summary>
    public string OptimizationKey => GetType().Name;

    #endregion

    #region Presentation

    /// <summary>
    ///     Gets the visual representation (icon and localized text) of the risk level.
    /// </summary>
    public RiskVisual RiskVisual =>
        Risk switch
        {
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

    /// <summary>Gets structured metadata describing impacts, tradeoffs, restart needs, and sources.</summary>
    public virtual OptimizationMetadata Metadata => OptimizationMetadata.Empty;

    /// <summary>Declares an improved aspect for <see cref="Metadata"/>.</summary>
    protected static OptimizationImpact Improves(ImpactArea area) =>
        new(area, ImpactDirection.Improves);

    /// <summary>Declares a trade-off aspect for <see cref="Metadata"/>.</summary>
    protected static OptimizationImpact Costs(ImpactArea area) =>
        new(area, ImpactDirection.TradeOff);

    /// <summary>Declares a reference source with automatic host classification.</summary>
    protected static OptimizationSource Source(string title, string url, SourceKind? kind = null) =>
        OptimizationSource.From(title, url, kind);

    /// <summary>Gets the tag displays derived from <see cref="OptimizationTags" />.</summary>
    public IEnumerable<OptimizationTagDisplay> TagDisplays => Meta.Tags.ToDisplays();

    /// <summary>Gets localized impact chips for the details view.</summary>
    public IReadOnlyList<OptimizationImpactDisplay> ImpactDisplays => Metadata.Impacts.ToDisplays();

    /// <summary>Gets up to three impact chips for the summary card.</summary>
    public IReadOnlyList<OptimizationImpactDisplay> CardImpactDisplays =>
        Metadata.Impacts.Count <= 3
            ? ImpactDisplays
            : Metadata.Impacts.Take(3).ToList().ToDisplays();

    /// <summary>Gets whether this optimization documents an explicit trade-off.</summary>
    public bool HasTradeOff => !string.IsNullOrEmpty(TradeOff);

    /// <summary>Gets whether this optimization uses Group Policy keys.</summary>
    public virtual bool SetsGroupPolicy => Metadata.SetsGroupPolicy;

    /// <summary>Gets the restart requirement after applying this optimization.</summary>
    public RestartRequirement RequiresRestart => Metadata.RequiresRestart;

    /// <summary>Gets whether any restart or Explorer/sign-out action is required.</summary>
    public bool HasRestartRequirement => Metadata.RequiresRestart != RestartRequirement.None;

    /// <summary>Gets the localized restart action label, or empty when none is needed.</summary>
    public string RestartDisplay => Metadata.RequiresRestart.ToDisplay();

    /// <summary>Gets whether this optimization prompts for options before apply.</summary>
    public bool IsConfigurable => this is IConfigurableOptimization;

    /// <summary>Gets whether a currently applied configurable optimization can be reconfigured.</summary>
    public bool CanReconfigure => IsConfigurable && State.IsApplied;

    /// <summary>Gets whether applying directly should ask for user confirmation first.</summary>
    public bool RequiresConfirmation =>
        Risk == OptimizationRisk.Risky || Metadata.ConfirmBeforeApply;

    /// <summary>Gets the localized trade-off description, or empty when none is declared.</summary>
    public virtual string TradeOff =>
        Loc.Instance.TryGet($"{Prefix}.TradeOff", out var text) ? text : string.Empty;

    /// <summary>Gets the localized detailed description, falling back to <see cref="ShortDescription"/>.</summary>
    public virtual string DetailedDescription =>
        Loc.Instance.TryGet($"{Prefix}.DetailedDescription", out var text)
            ? text
            : ShortDescription;

    /// <summary>Builds a convention-keyed toggle option under <c>{Prefix}.Option.{key}</c>.</summary>
    protected OptimizationOption ToggleOption(
        string key,
        bool defaultValue,
        bool isAlreadyApplied = false
    )
    {
        var optionPrefix = $"{Prefix}.Option.{key}";
        var descriptionKey = $"{optionPrefix}.Description";
        var tradeOffKey = $"{optionPrefix}.TradeOff";

        return new OptimizationOption
        {
            Key = key,
            Kind = OptimizationOptionKind.Toggle,
            LabelKey = $"{optionPrefix}.Label",
            DescriptionKey = Loc.Instance.HasKey(descriptionKey) ? descriptionKey : null,
            TradeOffKey = Loc.Instance.HasKey(tradeOffKey) ? tradeOffKey : null,
            DefaultValue = defaultValue ? "true" : "false",
            IsAlreadyAppliedHint = isAlreadyApplied,
        };
    }

    /// <summary>Builds a convention-keyed single-choice option under <c>{Prefix}.Option.{key}</c>.</summary>
    protected OptimizationOption SingleChoiceOption(
        string key,
        string defaultValue,
        params string[] choiceValues
    )
    {
        var optionPrefix = $"{Prefix}.Option.{key}";
        var descriptionKey = $"{optionPrefix}.Description";

        return new OptimizationOption
        {
            Key = key,
            Kind = OptimizationOptionKind.SingleChoice,
            LabelKey = $"{optionPrefix}.Label",
            DescriptionKey = Loc.Instance.HasKey(descriptionKey) ? descriptionKey : null,
            DefaultValue = defaultValue,
            Choices =
            [
                .. choiceValues.Select(value =>
                {
                    var choiceDescriptionKey = $"{optionPrefix}.{value}.Description";
                    return new OptimizationOptionChoice(
                        value,
                        $"{optionPrefix}.{value}.Label",
                        Loc.Instance.HasKey(choiceDescriptionKey) ? choiceDescriptionKey : null
                    );
                }),
            ],
        };
    }

    private OptimizationState _state = new();

    /// <summary>Gets or sets the applied state and timing for this optimization.</summary>
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
            OnPropertyChanged(nameof(CanReconfigure));
        }
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName
            is not (
                nameof(OptimizationState.IsApplied)
                or nameof(OptimizationState.Presence)
                or nameof(OptimizationState.HasRevertData)
            )
        )
            return;

        // Forward state mutations as a State change so external subscribers that listen to
        // the optimization stay in sync even when the State instance is swapped out later.
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsConditionBlocked));
        OnPropertyChanged(nameof(CanReconfigure));
    }

    #endregion

    #region Localization
    /// <summary>Gets the full localization key prefix for this optimization.</summary>
    public string Prefix => $"Optimizer.{OwnerKey}.{OptimizationKey}";

    /// <summary>Gets the localization key prefix for progress messages.</summary>
    public string ProgressPrefix => $"{Prefix}.Progress";

    /// <summary>Gets the localization key prefix for error messages.</summary>
    public string ErrorPrefix => $"{Prefix}.Error";

    /// <summary>Gets the localized display name of the optimization.</summary>
    public virtual string Name => Loc.Instance[$"{Prefix}.Name"];

    /// <summary>Gets the localized short description of what this optimization does.</summary>
    public virtual string ShortDescription => Loc.Instance[$"{Prefix}.ShortDescription"];

    /// <summary>Gets the English name written to the log.</summary>
    public virtual string LogName => Loc.Invariant[$"Optimizer.{OwnerKey}.{OptimizationKey}.Name"];

    /// <summary>Gets the English short description written to the log.</summary>
    public virtual string LogShortDescription =>
        Loc.Invariant[$"Optimizer.{OwnerKey}.{OptimizationKey}.ShortDescription"];

    #endregion

    #region Condition

    /// <summary>
    ///     Gets the compatibility condition type declared in the
    ///     <see cref="OptimizationAttribute" /> (implementing <see cref="ICondition" />), or
    ///     <see langword="null" /> when always available.
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
    ///     Gets a value that indicates whether this optimization should be presented in the unsupported state.
    /// </summary>
    public bool IsConditionBlocked =>
        ConditionResult.IsBlocking
        && !State.IsApplied
        && !State.HasRevertData
        && !IsConditionHidden;

    #endregion

    /// <inheritdoc />
    public virtual Task<bool?> IsSystemAtTargetAsync(
        SystemInfo snapshot,
        IReadOnlyDictionary<string, string>? appliedOptions = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult<bool?>(null);

    /// <inheritdoc />
    public abstract Task<ApplyResult> ApplyAsync(
        IProgress<ProcessingProgress> progress,
        OptimizationContext context
    );
}
