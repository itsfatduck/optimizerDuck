using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.UI.ViewModels.ToggleFeatures;

namespace optimizerDuck.Core.Models.ToggleFeatures;

public partial class FeatureSection : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FeatureViewModel> _features = [];
}
