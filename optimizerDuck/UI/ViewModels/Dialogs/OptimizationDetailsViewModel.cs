using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
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
