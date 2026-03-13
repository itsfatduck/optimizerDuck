using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Features;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Services.Managers;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.UI.Views.Pages.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Features;

[FeatureCategory(PageType = typeof(SystemFeatureCategory))]
public class SystemFeatures : IFeatureCategory
{
    private enum Sections
    {
        Boot,
        Input
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
                DefaultValue = "0"
            },
            new()
            {
                Path = @"HKCU\\Control Panel\\Keyboard",
                Name = "InitialKeyboardIndicators",
                OnValue = "2",
                OffValue = "0",
                DefaultValue = "0"
            }
        ];
    }
}
