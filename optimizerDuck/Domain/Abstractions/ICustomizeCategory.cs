using System.Collections.ObjectModel;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.UI;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Abstractions;

public interface ICustomizeCategory
{
    public string Name { get; }
    public string Description { get; }
    public SymbolRegular Icon { get; init; }
    public CustomizeOrder Order { get; init; }
    public ObservableCollection<ICustomizeSetting> Features { get; init; }
}
