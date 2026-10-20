using System.Collections.ObjectModel;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.UI;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Abstractions;

/// <summary>
///     Defines a category that groups related customization settings together.
/// </summary>
public interface ICustomizeCategory
{
    /// <summary>Gets the localized display name of the category.</summary>
    public string Name { get; }

    /// <summary>Gets the localized description of what this category configures.</summary>
    public string Description { get; }

    /// <summary>Gets the icon symbol displayed for this category in the UI.</summary>
    public SymbolRegular Icon { get; init; }

    /// <summary>Gets the display order index used to sort categories.</summary>
    public CustomizeOrder Order { get; init; }

    /// <summary>Gets the collection of customization settings contained in this category.</summary>
    public ObservableCollection<ICustomizeSetting> Features { get; init; }
}
