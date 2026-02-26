using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Managers;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Dialogs;

public partial class OptimizationDetailsViewModel(
    IOptimization optimization,
    ISnackbarService snackbarService,
    ILogger logger) : ObservableObject
{
    public IOptimization Optimization { get; } = optimization;

    [RelayCommand]
    private async Task OpenRevertFileAsync()
    {
        var revertData = await RevertManager.IsAppliedAsync(Optimization.Id);
        if (!revertData) return;

        try
        {
            var filePath = Path.Combine(Shared.RevertDirectory, Optimization.Id + ".json");
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            snackbarService.Show(
                Translations.Snackbar_OpenFailed_Title,
                Translations.Snackbar_OpenFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
            logger.LogError(ex, "Failed to open revert file for optimization {Id}", Optimization.Id);
        }
    }

    [RelayCommand]
    private async Task ViewSourceOnGitHubAsync()
    {
        if (Optimization is not Core.Optimizers.BaseOptimization baseOpt || baseOpt.OwnerType == null)
            return;

        var fileName = baseOpt.OwnerType.Name;
        var className = Optimization.OptimizationKey;
        var relativePath = $"optimizerDuck/Core/Optimizers/{fileName}.cs";
        var url = $"{Shared.GitHubRepoURL}/blob/master/{relativePath}";

        // Fetch source from GitHub raw content to find the class line number
        try
        {
            var rawUrl = $"https://raw.githubusercontent.com/itsfatduck/optimizerDuck/master/{relativePath}";

            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            string source = await httpClient.GetStringAsync(rawUrl);

            var lines = source.Split('\n');
            for (var i = 0; i < lines.Length; i++)
                if (lines[i].Contains($"class {className}", StringComparison.OrdinalIgnoreCase))
                {
                    url += $"#L{i + 1}";
                    break;
                }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch source to find line number for {Class}", className);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open GitHub URL: {Url}", url);
            snackbarService.Show(
                Translations.Snackbar_OpenLinkFailed_Title,
                Translations.Snackbar_OpenLinkFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5));
        }
    }
}