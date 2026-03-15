using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.UI.ViewModels.Features;

namespace optimizerDuck.Domain.Features.Models;

public partial class FeatureSection : ObservableObject
{
    [ObservableProperty] private ObservableCollection<FeatureViewModel> _features = [];

    [ObservableProperty] private string _name = string.Empty;
}