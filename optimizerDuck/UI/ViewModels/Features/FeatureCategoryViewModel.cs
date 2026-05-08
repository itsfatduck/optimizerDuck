using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Features.Models;
using optimizerDuck.Resources.Languages;
using optimizerDuck.UI.ViewModels.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

public partial class FeatureCategoryViewModel : ViewModel
{
    private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly ConcurrentDictionary<
        string,
        (string Content, DateTime FetchedAt)
    > _sourceCache = new();
    private static readonly TimeSpan SourceCacheTtl = TimeSpan.FromMinutes(5);

    private readonly List<FeatureViewModel> _allFeatures = [];

    private readonly IFeatureCategory? _currentCategory;

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
    private ObservableCollection<FeatureSection> _sections = [];

    [ObservableProperty]
    private int _selectedSortByIndex;

    public FeatureCategoryViewModel(IFeatureCategory category, ILoggerFactory loggerFactory)
    {
        _currentCategory = category;

        CategoryName = _currentCategory.Name;
        CategoryDescription = _currentCategory.Description;
        CategoryIcon = _currentCategory.Icon;

        _allFeatures.Clear();
        foreach (var feature in _currentCategory.Features)
            _allFeatures.Add(new FeatureViewModel(feature, loggerFactory));

        foreach (var feature in _allFeatures)
            _ = feature.LoadStateAsync();

        ApplyFilters();
        IsLoading = false;
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();
    }

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

        var filtered = _allFeatures.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(f =>
                f.Name.Contains(search, StringComparison.InvariantCultureIgnoreCase)
                || f.Description.Contains(search, StringComparison.InvariantCultureIgnoreCase)
            );
        }

        var sortedFeatures = SelectedSortByIndex switch
        {
            1 => filtered.OrderByDescending(f => f.IsEnabled).ThenBy(f => f.Name).ToList(),
            _ => filtered.OrderBy(f => f.Name).ToList(),
        };

        var grouped = sortedFeatures
            .GroupBy(f => string.IsNullOrEmpty(f.Section) ? Translations.Common_Other : f.Section)
            .OrderBy(g => g.Key == Translations.Common_Other ? 1 : 0)
            .ThenBy(g => g.Key);

        var sections = new ObservableCollection<FeatureSection>();
        foreach (var group in grouped)
        {
            var section = new FeatureSection
            {
                Name = group.Key,
                Features = new ObservableCollection<FeatureViewModel>([.. group]),
            };
            sections.Add(section);
        }

        Sections = sections;
    }

    [RelayCommand]
    private async Task ViewSourceOnGitHubAsync(FeatureViewModel featureViewModel)
    {
        if (featureViewModel is null)
            return;

        if (featureViewModel.Feature is not BaseFeature baseFeature || baseFeature.OwnerType == null)
            return;

        var fileName = baseFeature.OwnerType.Name;
        var className = baseFeature.FeatureKey;
        var namespacePath = (baseFeature.OwnerType.Namespace ?? string.Empty).Replace('.', '/');
        var relativePath = $"{namespacePath}/{fileName}.cs";
        var url = $"{Shared.GitHubRepoURL}/blob/master/{relativePath}";

        // Fetch source from GitHub raw content to find the class line number
        try
        {
            var rawUrl =
                $"https://raw.githubusercontent.com/itsfatduck/optimizerDuck/master/{relativePath}";

            string source;
            if (
                _sourceCache.TryGetValue(rawUrl, out var cached)
                && DateTime.UtcNow - cached.FetchedAt < SourceCacheTtl
            )
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
                if (lines[i].Contains($"class {className} : {nameof(BaseFeature)}", StringComparison.OrdinalIgnoreCase))
                {
                    url += $"#L{i + 1}";
                    break;
                }
        }
        catch (Exception ex)
        {
            // Log warning but still try to open the URL without line number
            System.Diagnostics.Debug.WriteLine(
                $"Could not fetch source to find line number for {className}: {ex.Message}"
            );
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
