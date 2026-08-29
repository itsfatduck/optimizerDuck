using System.Collections.ObjectModel;
using System.ComponentModel;
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

    [ObservableProperty]
    private string _categoryDescription = string.Empty;

    [ObservableProperty]
    private SymbolRegular _categoryIcon;

    [ObservableProperty]
    private string _categoryName = string.Empty;

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
    private int _unsupportedSettingsCount;

    [ObservableProperty]
    private string _unsupportedSettingsHeader = string.Empty;

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

        CategoryName = _currentCategory.Name;
        CategoryDescription = _currentCategory.Description;
        CategoryIcon = _currentCategory.Icon;

        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();

        _allSettings.Clear();
        foreach (var setting in _currentCategory.Features)
            _allSettings.Add(new CustomizeItemViewModel(setting, loggerFactory, registryWatcher));
    }

    protected override async Task InitializeOnceAsync()
    {
        var snapshot = await _systemInfoService.EnsureSnapshotAsync();
        if (ReferenceEquals(snapshot, SystemSnapshot.Unknown))
            _logger.LogWarning(
                "System snapshot is unavailable; conditions fail open for this session"
            );

        // Keep all collection/UI mutation on the UI thread, consistent with the
        // optimization category view model.
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
    private void EvaluateConditions(SystemSnapshot snapshot)
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
    ///     Recomputes the unsupported partition from the master settings list so settings that
    ///     fail their condition are shown under the expander instead of the main category
    ///     content. Idempotent: safe to call repeatedly as conditions are re-evaluated.
    /// </summary>
    private void PartitionUnsupportedSettings()
    {
        _unsupportedSettings.Clear();
        _unsupportedSettings.AddRange(_allSettings.Where(static s => s.IsUnsupported));
    }

    /// <summary>
    ///     Groups the unsupported settings into the same section structure used by the main
    ///     category content, so the expander can reuse the existing card templates.
    /// </summary>
    private void BuildUnsupportedSections()
    {
        UnsupportedSections = GroupIntoSections(_unsupportedSettings);
        UnsupportedSettingsCount = _unsupportedSettings.Count;
        HasUnsupportedSettings = _unsupportedSettings.Count > 0;
        UnsupportedSettingsHeader = string.Format(
            Loc.Instance["Customize.UI.UnsupportedSettings.Header"],
            UnsupportedSettingsCount
        );
    }

    /// <summary>
    ///     Groups settings into ordered sections, placing settings without a section under
    ///     the "Other" header. Shared by the main list and the unsupported expander.
    /// </summary>
    private static ObservableCollection<CustomizeSection> GroupIntoSections(
        IEnumerable<CustomizeItemViewModel> settings
    )
    {
        return new ObservableCollection<CustomizeSection>(
            settings
                .GroupBy(f =>
                    string.IsNullOrEmpty(f.Section) ? Translations.Common_Other : f.Section
                )
                .OrderBy(g => g.Key == Translations.Common_Other ? 1 : 0)
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

    public override Task OnNavigatedToAsync()
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        ApplicationThemeManager.Changed += OnThemeChanged;

        _systemInfoService.SnapshotRefreshed += OnSnapshotRefreshed;
        Loc.Instance.PropertyChanged += OnCultureChanged;

        return base.OnNavigatedToAsync();
    }

    public override Task OnNavigatedFromAsync()
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        _systemInfoService.SnapshotRefreshed -= OnSnapshotRefreshed;
        Loc.Instance.PropertyChanged -= OnCultureChanged;

        return base.OnNavigatedFromAsync();
    }

    /// <summary>
    ///     Re-evaluates every condition and rebuilds the filtered/unsupported partitions
    ///     when the system snapshot is refreshed (e.g. hardware changed) or the UI
    ///     language changes. Marshalled to the UI thread by <see cref="UiThread"/>.
    /// </summary>
    private void OnSnapshotRefreshed(object? sender, SystemSnapshot snapshot) =>
        ReEvaluateConditions(snapshot);

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs e) =>
        ReEvaluateConditions(_systemInfoService.Snapshot);

    private void ReEvaluateConditions(SystemSnapshot snapshot)
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
