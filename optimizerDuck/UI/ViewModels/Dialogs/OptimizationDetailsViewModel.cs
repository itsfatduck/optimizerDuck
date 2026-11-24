using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public enum DetailsDialogAction
{
    None,
    Reconfigure,
    RevertRemaining,
    ClearStaleRevert,
}

public enum SelectedOptionState
{
    Enabled,
    Disabled,
    Choice,
}

public sealed record SelectedOptionDisplay(
    string Label,
    string Value,
    SelectedOptionState State,
    SymbolRegular Icon
);

public partial class OptimizationDetailsViewModel(
    IOptimization optimization,
    SystemInfo snapshot,
    ISnackbarService snackbarService,
    ILogger logger
) : LocalizedObject
{
    public IOptimization Optimization { get; } = optimization;

    public DetailsDialogAction PendingAction { get; private set; } = DetailsDialogAction.None;

    public event Action? CloseRequested;

    /// <summary>
    ///     Holds the item's last recorded run, read once when the dialog is constructed.
    /// </summary>
    private readonly ChangeRecord? _record = ChangeRecordStore.TryRead(optimization.Id);
    private IReadOnlyList<ChangeRecordStepViewModel>? _steps;
    private IReadOnlyList<RecordFieldViewModel>? _recordFacts;
    private IReadOnlyList<SelectedOptionDisplay>? _selectedOptions;

    private BaseOptimization? BaseOpt => Optimization as BaseOptimization;

    public OptimizationMetadata Metadata => BaseOpt?.Metadata ?? OptimizationMetadata.Empty;

    public IEnumerable<OptimizationTagDisplay> TagDisplays => BaseOpt?.TagDisplays ?? [];

    public IReadOnlyList<OptimizationImpactDisplay> ImpactDisplays => BaseOpt?.ImpactDisplays ?? [];

    public bool HasImpacts => ImpactDisplays.Count > 0;

    public bool HasTradeOff => BaseOpt?.HasTradeOff == true;

    public string TradeOff => BaseOpt?.TradeOff ?? string.Empty;

    public string DetailedDescription =>
        BaseOpt?.DetailedDescription ?? Optimization.ShortDescription;

    public bool SetsGroupPolicy => BaseOpt?.SetsGroupPolicy == true;

    public bool HasRestartRequirement => Metadata.RequiresRestart != RestartRequirement.None;

    public string RestartDisplay => BaseOpt?.RestartDisplay ?? string.Empty;

    public IReadOnlyList<OptimizationSource> Sources => Metadata.Sources;

    public bool HasSources => Sources.Count > 0;

    public bool IsAppliedState => Optimization.State.IsApplied;

    public bool IsAlreadyOptimal => Optimization.State.IsAlreadyOptimal;

    public bool IsDrifted => Optimization.State.IsDrifted;

    public bool CanReconfigure => BaseOpt?.CanReconfigure == true;

    public bool ShowRevertFileAction => Optimization.State.HasRevertData;

    public IReadOnlyList<SelectedOptionDisplay> SelectedOptions =>
        _selectedOptions ??= BuildSelectedOptions();

    public bool HasSelectedOptions => SelectedOptions.Count > 0;

    /// <summary>
    ///     Gets the steps of the recorded run, worded for the run they belong to so a revert
    ///     reads as what it put back.
    /// </summary>
    public IReadOnlyList<ChangeRecordStepViewModel> Steps =>
        _steps ??= [
            .. (_record?.Steps ?? []).Select(step => new ChangeRecordStepViewModel(
                step,
                _record?.Operation ?? ChangeRecordOperation.Apply
            )),
        ];

    /// <summary>Gets a value that indicates whether a record is available to show.</summary>
    public bool HasRecord => Steps.Count > 0;

    /// <summary>
    ///     Gets a line naming the run the record describes and when it ran, so the dialog says up
    ///     front whether the item was last applied or reverted.
    /// </summary>
    public string RecordHeadline =>
        _record switch
        {
            { Operation: ChangeRecordOperation.Revert } => Loc.Instance[
                "Optimizer.Details.Record.Revert",
                _record.RevertedAt ?? _record.AppliedAt
            ],
            { } apply => Loc.Instance["Optimizer.Details.Record.Apply", apply.AppliedAt],
            _ => string.Empty,
        };

    /// <summary>Gets a value that indicates whether the record describes a revert.</summary>
    public bool RecordIsRevert => _record?.Operation == ChangeRecordOperation.Revert;

    /// <summary>
    ///     Gets the facts of the run itself: the build that wrote the record, the Windows version
    ///     it ran on and how long it took. A record written by an earlier build carries none of
    ///     these and shows no chip for them.
    /// </summary>
    public IReadOnlyList<RecordFieldViewModel> RecordFacts =>
        _recordFacts ??= BuildRecordFacts(_record);

    /// <summary>
    ///     Gets a value that indicates whether the record carries any fact about the run itself.
    /// </summary>
    public bool HasRecordFacts => RecordFacts.Count > 0;

    private IReadOnlyList<SelectedOptionDisplay> BuildSelectedOptions()
    {
        if (
            (!Optimization.State.IsApplied && !Optimization.State.HasRevertData)
            || Optimization is not IConfigurableOptimization configurable
            || _record
                is not {
                    Operation: ChangeRecordOperation.Apply,
                    SelectedOptions.Count: > 0
                } savedRecord
        )
            return [];

        var saved = savedRecord.SelectedOptions;
        var options = configurable.GetOptions(snapshot, saved);
        var list = new List<SelectedOptionDisplay>();
        foreach (var opt in options)
        {
            if (opt.Kind == OptimizationOptionKind.Toggle)
            {
                var enabled = saved.GetBool(
                    opt.Key,
                    bool.TryParse(opt.DefaultValue, out var d) && d
                );
                list.Add(
                    new SelectedOptionDisplay(
                        Loc.Instance[opt.LabelKey],
                        Loc.Instance[
                            enabled ? "Optimizer.Options.Enabled" : "Optimizer.Options.Disabled"
                        ],
                        enabled ? SelectedOptionState.Enabled : SelectedOptionState.Disabled,
                        enabled ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.SubtractCircle24
                    )
                );
            }
            else
            {
                var raw = saved.GetString(opt.Key, opt.DefaultValue);
                var choice = opt.Choices.FirstOrDefault(c =>
                    string.Equals(c.Value, raw, StringComparison.OrdinalIgnoreCase)
                );
                var valueLabel = choice is not null ? Loc.Instance[choice.LabelKey] : raw;
                list.Add(
                    new SelectedOptionDisplay(
                        Loc.Instance[opt.LabelKey],
                        valueLabel,
                        SelectedOptionState.Choice,
                        SymbolRegular.CheckmarkCircle24
                    )
                );
            }
        }

        return list;
    }

    private static IReadOnlyList<RecordFieldViewModel> BuildRecordFacts(ChangeRecord? record)
    {
        if (record is null)
            return [];

        var facts = new List<RecordFieldViewModel>();

        if (!string.IsNullOrEmpty(record.AppVersion))
            facts.Add(
                new RecordFieldViewModel(
                    SymbolRegular.Info24,
                    Loc.Instance["Optimizer.Details.Record.Version"],
                    record.AppVersion
                )
            );

        if (!string.IsNullOrEmpty(record.WindowsVersion))
            facts.Add(
                new RecordFieldViewModel(
                    SymbolRegular.Desktop24,
                    Loc.Instance["Optimizer.Details.Record.Windows"],
                    record.WindowsVersion
                )
            );

        if (record.ElapsedMs is { } elapsedMs)
            facts.Add(
                new RecordFieldViewModel(
                    SymbolRegular.Timer24,
                    Loc.Instance["Optimizer.Details.Record.Took"],
                    ChangeRecordStepViewModel.DescribeDuration(elapsedMs)
                )
            );

        return facts;
    }

    [RelayCommand]
    private void RequestReconfigure()
    {
        PendingAction = DetailsDialogAction.Reconfigure;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void RequestRevertRemaining()
    {
        PendingAction = DetailsDialogAction.RevertRemaining;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void RequestClearStaleRevert()
    {
        PendingAction = DetailsDialogAction.ClearStaleRevert;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenSourceUrl(string? url)
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
        )
            return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Loc.Instance["Snackbar.OpenLinkFailed.Title"],
                Loc.Instance["Snackbar.OpenLinkFailed.Message"],
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            logger.LogWarning(ex, "Failed to open reference source URL: {Url}", url);
        }
    }

    [RelayCommand]
    private async Task OpenRevertFileAsync()
    {
        if (!await RevertManager.IsAppliedAsync(Optimization.Id))
            return;

        RevealInExplorer(
            Path.Combine(Shared.RevertDirectory, Optimization.Id + ".json"),
            "revert file"
        );
    }

    /// <summary>Reveals the file the record of the last run was written to.</summary>
    [RelayCommand]
    private void OpenRecordFile()
    {
        var filePath = ChangeRecordStore.PathFor(Optimization.Id);
        if (!File.Exists(filePath))
            return;

        RevealInExplorer(filePath, "change record");
    }

    private void RevealInExplorer(string filePath, string logContext)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true,
                }
            );
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Loc.Instance["Snackbar.OpenFailed.Title"],
                Loc.Instance["Snackbar.OpenFailed.Message"],
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            logger.LogError(
                ex,
                "Failed to open {Context} for optimization {Id}",
                logContext,
                Optimization.Id
            );
        }
    }

    [RelayCommand]
    private async Task ViewSourceOnGitHubAsync()
    {
        if (Optimization is not BaseOptimization baseOpt || baseOpt.OwnerType == null)
            return;

        await GitHubSourceHelper.OpenSourceOnGitHubAsync(
            baseOpt.OwnerType,
            Optimization.OptimizationKey,
            logger: logger,
            snackbarService: snackbarService
        );
    }
}
