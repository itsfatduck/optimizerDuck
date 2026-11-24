using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.Revert;
using optimizerDuck.UI.Dialogs;
using optimizerDuck.UI.ViewModels.Dialogs;
using Wpf.Ui;
using Wpf.Ui.Controls;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace optimizerDuck.UI.ViewModels.Pages;

/// <summary>
///     ViewModel for managing retired optimizations that still retain revert backups on disk.
/// </summary>
public partial class LegacyRevertsViewModel(
    OptimizationRegistry optimizationRegistry,
    RevertManager revertManager,
    IContentDialogService contentDialogService,
    ISnackbarService snackbarService,
    ILogger<LegacyRevertsViewModel> logger
) : ViewModel
{
    public ObservableCollection<LegacyRevertItem> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public bool HasItems => Items.Count > 0;

    public string CountSummary => Loc.Instance["Optimizer.Legacy.CountSummary", Items.Count];

    protected override async Task InitializeOnceAsync()
    {
        await optimizationRegistry.EnsurePreloadedAsync().ConfigureAwait(true);
        SyncItems();
    }

    public override async Task OnNavigatedToAsync()
    {
        var wasInitialized = IsInitialized;
        optimizationRegistry.LegacyRevertsChanged -= OnLegacyRevertsChanged;
        optimizationRegistry.LegacyRevertsChanged += OnLegacyRevertsChanged;

        await base.OnNavigatedToAsync();

        if (wasInitialized && optimizationRegistry.IsPreloaded)
        {
            await optimizationRegistry.RefreshLegacyRevertsAsync().ConfigureAwait(true);
            SyncItems();
        }
    }

    public override Task OnNavigatedFromAsync()
    {
        optimizationRegistry.LegacyRevertsChanged -= OnLegacyRevertsChanged;
        return base.OnNavigatedFromAsync();
    }

    [RelayCommand]
    private async Task RevertItemAsync(LegacyRevertItem? item)
    {
        if (item is null || IsBusy)
            return;

        IsBusy = true;
        var progressVm = new ProcessingViewModel();
        var dialog = new ContentDialog
        {
            Title = item.DisplayName,
            Content = new ProcessingDialog { DataContext = progressVm },
            IsFooterVisible = false,
        };

        _ = contentDialogService.ShowAsync(dialog, CancellationToken.None);
        try
        {
            var result = await revertManager.RevertAsync(
                item.AsOptimization(),
                progressVm.ProgressReporter
            );

            if (result.Success)
            {
                optimizationRegistry.RemoveLegacyRevertItem(item.Id);
                if (Application.Current is App app)
                    app.HasPendingChanges = true;

                ShowSnackbar(
                    Loc.Instance["Optimization.Revert.Snackbar.Success.Title"],
                    Loc.Instance["Optimizer.Legacy.RevertSuccess", item.DisplayName],
                    ControlAppearance.Success,
                    SymbolRegular.CheckmarkCircle24
                );
            }
            else
            {
                if (!result.AllStepsFailed)
                    await optimizationRegistry.RefreshLegacyRevertsAsync().ConfigureAwait(true);

                ShowSnackbar(
                    Loc.Instance["Optimization.Revert.Snackbar.Error.Title"],
                    Loc.Instance["Optimizer.Legacy.RevertFailed", result.Message],
                    ControlAppearance.Danger,
                    SymbolRegular.ErrorCircle24
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revert legacy optimization {Id}", item.Id);
            ShowSnackbar(
                Loc.Instance["Optimization.Revert.Snackbar.Error.Title"],
                Loc.Instance["Optimizer.Legacy.RevertFailed", ex.Message],
                ControlAppearance.Danger,
                SymbolRegular.ErrorCircle24
            );
        }
        finally
        {
            dialog.Hide();
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearRecordAsync(LegacyRevertItem? item)
    {
        if (item is null || IsBusy)
            return;

        var dialog = new ContentDialog
        {
            Title = item.DisplayName,
            Content = new TextBlock
            {
                FontTypography = FontTypography.Body,
                Text = Loc.Instance["Optimizer.Legacy.ClearRecord.Confirm"],
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = Loc.Instance["Optimizer.Legacy.ClearRecord"],
            PrimaryButtonAppearance = ControlAppearance.Danger,
            CloseButtonText = Loc.Instance["Button.Cancel"],
            DefaultButton = ContentDialogButton.Close,
            MaxWidth = 480,
        };

        var result = await contentDialogService.ShowAsync(dialog, CancellationToken.None);
        if (result != ContentDialogResult.Primary)
            return;

        revertManager.RemoveRevertData(item.Id, item.OptimizationKey);
        optimizationRegistry.RemoveLegacyRevertItem(item.Id);
    }

    [RelayCommand]
    private void OpenRevertFile(LegacyRevertItem? item)
    {
        if (item is null)
            return;

        var filePath = Path.Combine(Shared.RevertDirectory, $"{item.Id}.json");
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
            ShowSnackbar(
                Loc.Instance["Snackbar.OpenFailed.Title"],
                Loc.Instance["Snackbar.OpenFailed.Message"],
                ControlAppearance.Danger,
                SymbolRegular.ErrorCircle24
            );
            logger.LogError(ex, "Failed to open legacy revert file {Id}", item.Id);
        }
    }

    private void OnLegacyRevertsChanged() => _ = UiThread.InvokeAsync(SyncItems);

    private void SyncItems()
    {
        Items.Clear();
        foreach (var item in optimizationRegistry.LegacyRevertItems)
            Items.Add(item);

        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CountSummary));
    }

    private void ShowSnackbar(
        string title,
        string message,
        ControlAppearance appearance,
        SymbolRegular icon
    )
    {
        snackbarService.Show(
            title,
            message,
            appearance,
            new SymbolIcon { Symbol = icon, Filled = true },
            TimeSpan.FromSeconds(5)
        );
    }
}
