using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Services.Configuration;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Dialogs;

/// <summary>Represents the confirmed user action from <c>OptimizationOptionsDialog</c>.</summary>
public enum OptimizationOptionsAction
{
    Cancelled,
    Apply,
    Revert,
}

/// <summary>Outcome and selected option values returned by <c>OptimizationOptionsDialog</c>.</summary>
public sealed record OptimizationOptionsResult(
    OptimizationOptionsAction Action,
    IReadOnlyDictionary<string, string>? SelectedOptions = null
);

/// <summary>
///     ViewModel backing the pre-apply configuration dialog for <see cref="IConfigurableOptimization"/>.
/// </summary>
public partial class OptimizationOptionsViewModel : LocalizedObject
{
    public OptimizationOptionsViewModel(
        IOptimization optimization,
        IReadOnlyList<OptimizationOption> options,
        bool requiresAtLeastOneSelection,
        bool isReconfiguring = false,
        IReadOnlyDictionary<string, string>? previousSelections = null
    )
    {
        Optimization = optimization;
        RequiresAtLeastOneSelection = requiresAtLeastOneSelection;
        IsReconfiguring = isReconfiguring;
        Options =
        [
            .. options.Select(option =>
            {
                var isOwnedByCurrentApply =
                    isReconfiguring
                    && (previousSelections is null || previousSelections.GetBool(option.Key));
                var isLockedAlreadySet =
                    option.Kind == OptimizationOptionKind.Toggle
                    && option.IsAlreadyAppliedHint
                    && !isOwnedByCurrentApply;
                return new OptimizationOptionItemViewModel(
                    option,
                    isLockedAlreadySet,
                    OnSelectionChanged
                );
            }),
        ];
    }

    public IOptimization Optimization { get; }

    public bool RequiresAtLeastOneSelection { get; }

    public bool IsReconfiguring { get; }

    public ObservableCollection<OptimizationOptionItemViewModel> Options { get; }

    public bool HasTradeOff => Optimization is BaseOptimization { HasTradeOff: true };

    public string TradeOff => Optimization is BaseOptimization bo ? bo.TradeOff : string.Empty;

    public bool HasAnySelection =>
        Options.Any(static item =>
            !item.IsLockedAlreadySet
            && (item.IsToggle ? item.BoolValue : !string.IsNullOrEmpty(item.SelectedChoiceValue))
        );

    public bool AllOptionsAlreadySet =>
        Options.Count > 0 && Options.All(static item => item.IsLockedAlreadySet);

    public bool WillRevertOnConfirm =>
        IsReconfiguring && RequiresAtLeastOneSelection && !HasAnySelection;

    public bool CanApply => !RequiresAtLeastOneSelection || IsReconfiguring || HasAnySelection;

    public bool ShowAllAlreadySetHint =>
        RequiresAtLeastOneSelection && !IsReconfiguring && AllOptionsAlreadySet;

    public bool ShowSelectAtLeastOneHint =>
        RequiresAtLeastOneSelection
        && !IsReconfiguring
        && !AllOptionsAlreadySet
        && !HasAnySelection;

    public bool ShowWillRevertHint => WillRevertOnConfirm;

    public string PrimaryButtonText =>
        WillRevertOnConfirm
            ? Loc.Instance["Optimizer.Options.Revert"]
            : Loc.Instance["Optimizer.Options.Apply"];

    public ControlAppearance PrimaryButtonAppearance =>
        WillRevertOnConfirm ? ControlAppearance.Caution : ControlAppearance.Primary;

    /// <summary>Builds the final confirmed action and selected option map.</summary>
    public OptimizationOptionsResult BuildConfirmResult() =>
        WillRevertOnConfirm
            ? new(OptimizationOptionsAction.Revert)
            : new(OptimizationOptionsAction.Apply, BuildSelectedOptions());

    /// <summary>Builds the final key/value map passed into <see cref="OptimizationContext.Options"/>.</summary>
    public IReadOnlyDictionary<string, string> BuildSelectedOptions()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Options)
        {
            result[item.Key] = item.IsToggle
                ? (!item.IsLockedAlreadySet && item.BoolValue ? "true" : "false")
                : item.SelectedChoiceValue;
        }

        return result;
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(HasAnySelection));
        OnPropertyChanged(nameof(WillRevertOnConfirm));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(ShowSelectAtLeastOneHint));
        OnPropertyChanged(nameof(ShowWillRevertHint));
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(PrimaryButtonAppearance));
    }
}

/// <summary>Wraps one configurable question (toggle or radio-group).</summary>
public partial class OptimizationOptionItemViewModel : LocalizedObject
{
    private readonly Action _onChanged;

    public OptimizationOptionItemViewModel(
        OptimizationOption option,
        bool isLockedAlreadySet,
        Action onChanged
    )
    {
        _onChanged = onChanged;
        Key = option.Key;
        IsToggle = option.Kind == OptimizationOptionKind.Toggle;
        IsSingleChoice = option.Kind == OptimizationOptionKind.SingleChoice;
        Label = Loc.Instance[option.LabelKey];
        Description = string.IsNullOrEmpty(option.DescriptionKey)
            ? string.Empty
            : Loc.Instance[option.DescriptionKey];
        TradeOff = string.IsNullOrEmpty(option.TradeOffKey)
            ? string.Empty
            : Loc.Instance[option.TradeOffKey];
        IsLockedAlreadySet = isLockedAlreadySet;
        GroupName = $"Option_{option.Key}_{GetHashCode()}";

        if (IsToggle)
        {
            _boolValue =
                !isLockedAlreadySet && bool.TryParse(option.DefaultValue, out var parsed) && parsed;
        }
        else
        {
            var selected = option.DefaultValue;
            Choices =
            [
                .. option.Choices.Select(choice => new OptimizationOptionChoiceViewModel(
                    choice,
                    string.Equals(choice.Value, selected, StringComparison.OrdinalIgnoreCase),
                    OnChoiceSelected
                )),
            ];
            if (Choices.All(static c => !c.IsSelected) && Choices.Count > 0)
                Choices[0].IsSelected = true;
        }
    }

    public string Key { get; }
    public bool IsToggle { get; }
    public bool IsSingleChoice { get; }
    public string Label { get; }
    public string Description { get; }
    public bool HasDescription => !string.IsNullOrEmpty(Description);
    public string TradeOff { get; }
    public bool HasTradeOff => !string.IsNullOrEmpty(TradeOff);
    public bool IsLockedAlreadySet { get; }
    public bool IsInteractiveToggle => IsToggle && !IsLockedAlreadySet;
    public bool ShowTradeOff => HasTradeOff && !IsLockedAlreadySet;
    public string GroupName { get; }

    public ObservableCollection<OptimizationOptionChoiceViewModel> Choices { get; } = [];

    [ObservableProperty]
    private bool _boolValue;

    public string SelectedChoiceValue =>
        Choices.FirstOrDefault(static c => c.IsSelected)?.Value ?? string.Empty;

    partial void OnBoolValueChanged(bool value)
    {
        if (IsLockedAlreadySet && value)
        {
            _boolValue = false;
            OnPropertyChanged(nameof(BoolValue));
            return;
        }

        _onChanged();
    }

    private void OnChoiceSelected(OptimizationOptionChoiceViewModel selectedChoice)
    {
        foreach (var choice in Choices)
        {
            if (!ReferenceEquals(choice, selectedChoice) && choice.IsSelected)
                choice.SetSelectedSilently(false);
        }

        OnPropertyChanged(nameof(SelectedChoiceValue));
        _onChanged();
    }
}

/// <summary>Wraps one radio option inside <see cref="OptimizationOptionKind.SingleChoice"/>.</summary>
public partial class OptimizationOptionChoiceViewModel : LocalizedObject
{
    private readonly Action<OptimizationOptionChoiceViewModel> _onSelected;
    private bool _suppressNotify;

    public OptimizationOptionChoiceViewModel(
        OptimizationOptionChoice choice,
        bool isSelected,
        Action<OptimizationOptionChoiceViewModel> onSelected
    )
    {
        _onSelected = onSelected;
        Value = choice.Value;
        Label = Loc.Instance[choice.LabelKey];
        Description = string.IsNullOrEmpty(choice.DescriptionKey)
            ? string.Empty
            : Loc.Instance[choice.DescriptionKey];
        _isSelected = isSelected;
    }

    public string Value { get; }
    public string Label { get; }
    public string Description { get; }
    public bool HasDescription => !string.IsNullOrEmpty(Description);

    [ObservableProperty]
    private bool _isSelected;

    internal void SetSelectedSilently(bool value)
    {
        _suppressNotify = true;
        IsSelected = value;
        _suppressNotify = false;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_suppressNotify && value)
            _onSelected(this);
    }
}
