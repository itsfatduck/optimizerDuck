using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Conditions;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;
using optimizerDuck.UI.ViewModels.Customize;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class CustomizeCategoryViewModel : ViewModel
{
    #region Observable Properties

    [ObservableProperty]
    private ApplicationTheme _currentApplicationTheme = ApplicationTheme.Unknown;

    public string CategoryDescription => _currentCategory?.Description ?? string.Empty;

    [ObservableProperty]
    private SymbolRegular _categoryIcon;

    public string CategoryName => _currentCategory?.Name ?? string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CustomizeSection> _sections = [];

    [ObservableProperty]
    private ObservableCollection<CustomizeSection> _unsupportedSections = [];

    [ObservableProperty]
    private bool _hasUnsupportedSettings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnsupportedSettingsHeader))]
    private int _unsupportedSettingsCount;

    public string UnsupportedSettingsHeader =>
        Loc.Instance["Customize.UI.UnsupportedSettings.Header", UnsupportedSettingsCount];

    [ObservableProperty]
    private int _selectedSortByIndex;

    #endregion

    #region Lifecycle

    private readonly List<CustomizeItemViewModel> _allSettings = [];
    private readonly List<CustomizeItemViewModel> _unsupportedSettings = [];
    private readonly ICustomizeCategory? _currentCategory;
    private readonly ILogger<CustomizeCategoryViewModel> _logger;
    private readonly SystemInfoService _systemInfoService;

    public CustomizeCategoryViewModel(
        ICustomizeCategory category,
        ILoggerFactory loggerFactory,
        IRegistryWatcher registryWatcher,
        SystemInfoService systemInfoService
    )
    {
        _currentCategory = category;
        _systemInfoService = systemInfoService;
        _logger = loggerFactory.CreateLogger<CustomizeCategoryViewModel>();

        CategoryIcon = _currentCategory.Icon;

        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();

        _allSettings.Clear();
        foreach (var setting in _currentCategory.Features)
            _allSettings.Add(new CustomizeItemViewModel(setting, loggerFactory, registryWatcher));
    }

    protected override async Task InitializeOnceAsync()
    {
        var snapshot = await _systemInfoService.EnsureSnapshotAsync();
        if (snapshot.IsUnknown)
            _logger.LogWarning(
                "System snapshot is unavailable; conditions fail open for this session"
            );

        // All collection and UI mutation stays on the UI thread.
        await UiThread.InvokeAsync(async () =>
        {
            EvaluateConditions(snapshot);
            PartitionUnsupportedSettings();

            await Task.WhenAll(_allSettings.Select(s => s.LoadStateAsync()));
            ApplyFilters();
            BuildUnsupportedSections();
        });
        IsLoading = false;
    }

    /// <summary>
    ///     Evaluates the compatibility condition of every setting against the system snapshot.
    /// </summary>
    private void EvaluateConditions(SystemInfo snapshot)
    {
        ConditionEvaluator.EvaluateAll(
            _allSettings,
            s => s.Setting.ConditionType,
            (s, r) => s.ConditionResult = r,
            snapshot,
            _logger
        );
    }

    /// <summary>
    ///     Recomputes the unsupported partition from the master list so settings that fail their
    ///     condition show under the expander. Idempotent: safe to call as conditions change.
    /// </summary>
    private void PartitionUnsupportedSettings()
    {
        _unsupportedSettings.Clear();
        _unsupportedSettings.AddRange(_allSettings.Where(static s => s.IsUnsupported));
    }

    /// <summary>
    ///     Builds the unsupported sections so the expander reuses the main card templates.
    /// </summary>
    private void BuildUnsupportedSections()
    {
        UnsupportedSections = GroupIntoSections(_unsupportedSettings);
        UnsupportedSettingsCount = _unsupportedSettings.Count;
        HasUnsupportedSettings = _unsupportedSettings.Count > 0;
    }

    /// <summary>
    ///     Groups settings into sections, placing settings without a section under the "Other"
    ///     header last.
    /// </summary>
    private static ObservableCollection<CustomizeSection> GroupIntoSections(
        IEnumerable<CustomizeItemViewModel> settings
    )
    {
        return new ObservableCollection<CustomizeSection>(
            settings
                .GroupBy(f =>
                    string.IsNullOrEmpty(f.Section) ? Loc.Instance["Common.Other"] : f.Section
                )
                .OrderBy(g => g.Key == Loc.Instance["Common.Other"] ? 1 : 0)
                .ThenBy(g => g.Key)
                .Select(g => new CustomizeSection
                {
                    Name = g.Key,
                    Features = new ObservableCollection<CustomizeItemViewModel>([.. g]),
                })
        );
    }

    private void OnThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        _ = UiThread.InvokeAsync(() =>
        {
            if (CurrentApplicationTheme != currentApplicationTheme)
                CurrentApplicationTheme = currentApplicationTheme;
        });
    }

    /// <inheritdoc />
    public override Task OnNavigatedToAsync()
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        ApplicationThemeManager.Changed += OnThemeChanged;

        _systemInfoService.SnapshotRefreshed += OnSnapshotRefreshed;

        return base.OnNavigatedToAsync();
    }

    /// <inheritdoc />
    public override Task OnNavigatedFromAsync()
    {
        _systemInfoService.SnapshotRefreshed -= OnSnapshotRefreshed;

        return base.OnNavigatedFromAsync();
    }

    /// <summary>
    ///     Re-evaluates every condition and rebuilds the partitions when the system snapshot is
    ///     refreshed.
    /// </summary>
    private void OnSnapshotRefreshed(object? sender, SystemInfo snapshot) =>
        ReEvaluateConditions(snapshot);

    /// <summary>
    ///     Re-evaluates every condition and rebuilds the partitions on a language change.
    /// </summary>
    protected override void OnLanguageChanged(CultureInfo newCulture) =>
        ReEvaluateConditions(_systemInfoService.Snapshot);

    private void ReEvaluateConditions(SystemInfo snapshot)
    {
        _ = UiThread.InvokeAsync(() =>
        {
            EvaluateConditions(snapshot);
            PartitionUnsupportedSettings();
            BuildUnsupportedSections();
            ApplyFilters();
        });
    }

    #endregion

    #region Filtering & Sorting

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedSortByIndexChanged(int value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (_currentCategory == null)
            return;

        // Master list keeps every setting; unsupported ones are partitioned out below.
        var filtered = _allSettings.Where(static f => !f.IsUnsupported).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(f =>
                f.Name.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase)
                || f.Description.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase)
            );
        }

        var sortedSettings = SelectedSortByIndex switch
        {
            1 => filtered.OrderByDescending(f => f.IsEnabled).ThenBy(f => f.Name),
            _ => filtered.OrderBy(f => f.Name),
        };

        Sections = GroupIntoSections(sortedSettings);
    }

    #endregion

    [RelayCommand]
    private async Task ViewSourceOnGitHubAsync(CustomizeItemViewModel itemViewModel)
    {
        if (itemViewModel is null)
            return;

        if (
            itemViewModel.Setting is not BaseCustomizeSetting baseSetting
            || baseSetting.OwnerType == null
        )
            return;

        await GitHubSourceHelper.OpenSourceOnGitHubAsync(
            baseSetting.OwnerType,
            baseSetting.FeatureKey,
            nameof(BaseCustomizeSetting)
        );
    }
}
