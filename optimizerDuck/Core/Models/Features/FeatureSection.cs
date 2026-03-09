using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.UI.ViewModels.Features;

namespace optimizerDuck.Core.Models.Features;

public partial class FeatureSection : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FeatureViewModel> _features = [];
}
