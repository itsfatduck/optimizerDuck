using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Revert;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public partial class OptimizationDetailsViewModel(
    IOptimization optimization,
    ISnackbarService snackbarService,
    ILogger logger
) : LocalizedObject
{
    public IOptimization Optimization { get; } = optimization;

    /// <summary>
    ///     What the item's last apply did, straight from the record on disk. Read once: a dialog
    ///     does not need to watch the file, and an item applied while it is open is reloaded by
    ///     opening it again.
    /// </summary>
    private readonly ChangeRecord? _record = ChangeRecordStore.TryRead(optimization.Id);
    private IReadOnlyList<ChangeRecordStepViewModel>? _steps;
    private IReadOnlyList<RecordFieldViewModel>? _recordFacts;

    /// <summary>
    ///     The steps of the recorded run, worded for the run they belong to, so a revert reads as
    ///     what it put back.
    /// </summary>
    public IReadOnlyList<ChangeRecordStepViewModel> Steps =>
        _steps ??= [
            .. (_record?.Steps ?? []).Select(step => new ChangeRecordStepViewModel(
                step,
                _record?.Operation ?? ChangeRecordOperation.Apply
            )),
        ];

    /// <summary>Whether there is a record to show at all.</summary>
    public bool HasRecord => Steps.Count > 0;

    /// <summary>
    ///     Which run this record describes and when it ran, so the dialog says up front whether the
    ///     last thing that happened to this item was an apply or a revert.
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

    /// <summary>Whether the record describes a revert, which the header marks.</summary>
    public bool RecordIsRevert => _record?.Operation == ChangeRecordOperation.Revert;

    /// <summary>
    ///     What the run itself was: the build that wrote the record and the Windows version it ran
    ///     on, so a record read out of the app states its own context, and how long the run took. A
    ///     record written by an earlier build carries none of these and shows no chip for them.
    /// </summary>
    public IReadOnlyList<RecordFieldViewModel> RecordFacts =>
        _recordFacts ??= BuildRecordFacts(_record);

    /// <summary>Whether the record carries any fact about the run itself.</summary>
    public bool HasRecordFacts => RecordFacts.Count > 0;

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
    private async Task OpenRevertFileAsync()
    {
        var revertData = await RevertManager.IsAppliedAsync(Optimization.Id);
        if (!revertData)
            return;

        try
        {
            var filePath = Path.Combine(Shared.RevertDirectory, Optimization.Id + ".json");
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
                "Failed to open revert file for optimization {Id}",
                Optimization.Id
            );
        }
    }

    /// <summary>Reveals the file the record of the last run was written to.</summary>
    [RelayCommand]
    private void OpenRecordFile()
    {
        var filePath = ChangeRecordStore.PathFor(Optimization.Id);
        if (!File.Exists(filePath))
            return;

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
                "Failed to open the change record for optimization {Id}",
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
