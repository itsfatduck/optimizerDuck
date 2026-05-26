using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Resources.Languages;
using optimizerDuck.UI.ViewModels.Customize;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class CustomizeCategoryViewModel : ViewModel
{
    #region Cache & Constants

    private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly ConcurrentDictionary<string, (string Content, DateTime FetchedAt)> _sourceCache = new();
    private static readonly TimeSpan SourceCacheTtl = TimeSpan.FromMinutes(5);

    #endregion

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
    private int _selectedSortByIndex;

    #endregion

    #region Lifecycle

    private readonly List<CustomizeItemViewModel> _allSettings = [];
    private readonly ICustomizeCategory? _currentCategory;

    public CustomizeCategoryViewModel(ICustomizeCategory category, ILoggerFactory loggerFactory)
    {
        _currentCategory = category;

        CategoryName = _currentCategory.Name;
        CategoryDescription = _currentCategory.Description;
        CategoryIcon = _currentCategory.Icon;

        CurrentApplicationTheme = ApplicationThemeManager.GetAppTheme();

        _allSettings.Clear();
        foreach (var setting in _currentCategory.Features)
            _allSettings.Add(new CustomizeItemViewModel(setting, loggerFactory));

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await Task.WhenAll(_allSettings.Select(s => s.LoadStateAsync()));
        ApplyFilters();
        IsLoading = false;
    }

    private void OnThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (CurrentApplicationTheme != currentApplicationTheme)
                CurrentApplicationTheme = currentApplicationTheme;
        });
    }

    public override Task OnNavigatedToAsync()
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        ApplicationThemeManager.Changed += OnThemeChanged;
        return base.OnNavigatedToAsync();
    }

    public override Task OnNavigatedFromAsync()
    {
        ApplicationThemeManager.Changed -= OnThemeChanged;
        return base.OnNavigatedFromAsync();
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

        var filtered = _allSettings.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(f =>
                f.Name.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase)
                || f.Description.Contains(SearchText, StringComparison.InvariantCultureIgnoreCase)
            );
        }

        var sortedSettings = SelectedSortByIndex switch
        {
            1 => filtered.OrderByDescending(f => f.IsEnabled).ThenBy(f => f.Name).ToList(),
            _ => filtered.OrderBy(f => f.Name).ToList(),
        };

        var grouped = sortedSettings
            .GroupBy(f => string.IsNullOrEmpty(f.Section) ? Translations.Common_Other : f.Section)
            .OrderBy(g => g.Key == Translations.Common_Other ? 1 : 0)
            .ThenBy(g => g.Key);

        var sections = new ObservableCollection<CustomizeSection>();
        foreach (var group in grouped)
        {
            var section = new CustomizeSection
            {
                Name = group.Key,
                Features = new ObservableCollection<CustomizeItemViewModel>([.. group]),
            };
            sections.Add(section);
        }

        Sections = sections;
    }

    #endregion

    [RelayCommand]
    private async Task ViewSourceOnGitHubAsync(CustomizeItemViewModel itemViewModel)
    {
        if (itemViewModel is null)
            return;

        if (itemViewModel.Setting is not BaseCustomizeSetting baseSetting || baseSetting.OwnerType == null)
            return;

        var fileName = baseSetting.OwnerType.Name;
        var className = baseSetting.FeatureKey;
        var namespacePath = (baseSetting.OwnerType.Namespace ?? string.Empty).Replace('.', '/');
        var relativePath = $"{namespacePath}/{fileName}.cs";
        var url = $"{Shared.GitHubRepoURL}/blob/master/{relativePath}";

        try
        {
            var rawUrl = $"https://raw.githubusercontent.com/itsfatduck/optimizerDuck/master/{relativePath}";

            string source;
            if (_sourceCache.TryGetValue(rawUrl, out var cached) && DateTime.UtcNow - cached.FetchedAt < SourceCacheTtl)
            {
                source = cached.Content;
            }
            else
            {
                source = await httpClient.GetStringAsync(rawUrl);
                _sourceCache[rawUrl] = (source, DateTime.UtcNow);
            }

            var lines = source.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains($"class {className} : {nameof(BaseCustomizeSetting)}", StringComparison.OrdinalIgnoreCase))
                {
                    url += $"#L{i + 1}";
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not fetch source to find line number for {className}: {ex.Message}");
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open GitHub URL: {url}. Error: {ex.Message}");
        }
    }
}
