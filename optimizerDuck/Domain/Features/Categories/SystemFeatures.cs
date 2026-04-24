using System.Collections.ObjectModel;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Features.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.Pages.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Features.Categories;

[FeatureCategory(PageType = typeof(SystemFeatureCategory))]
public class SystemFeatures : IFeatureCategory
{
    private enum Sections
    {
        Input,
    }

    public string Name => Loc.Instance[$"Features.{nameof(SystemFeatures)}.Name"];
    public string Description => Loc.Instance[$"Features.{nameof(SystemFeatures)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Desktop16;
    public FeatureCategoryOrder Order { get; init; } = FeatureCategoryOrder.System;
    public ObservableCollection<IFeature> Features { get; init; } = [];

    [Feature(Section = nameof(Sections.Input), Icon = SymbolRegular.NumberSymbol24)]
    public class EnableNumLockOnBoot : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKU\.DEFAULT\Control Panel\Keyboard",
                    Name = "InitialKeyboardIndicators",
                    OnValue = "2",
                    OffValue = "0",
                    DefaultValue = "0",
                },
                new()
                {
                    Path = @"HKCU\\Control Panel\\Keyboard",
                    Name = "InitialKeyboardIndicators",
                    OnValue = "2",
                    OffValue = "0",
                    DefaultValue = "0",
                },
            ];
    }
}
