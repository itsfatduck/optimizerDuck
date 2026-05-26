using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.UI.ViewModels.Customize;

namespace optimizerDuck.Domain.Customize.Models;

public partial class CustomizeSection : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<CustomizeItemViewModel> _features = [];

    [ObservableProperty]
    private string _name = string.Empty;
}
