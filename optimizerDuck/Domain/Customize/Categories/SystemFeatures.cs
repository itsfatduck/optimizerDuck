using System.Collections.ObjectModel;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.Pages.Customize;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Customize.Categories;

[CustomizeCategory(PageType = typeof(SystemFeatureCategory))]
public class SystemFeatures : ICustomizeCategory
{
    private enum Sections
    {
        Input,
        Services,
        Power,
    }

    public string Name => Loc.Instance[$"Customize.{nameof(SystemFeatures)}.Name"];
    public string Description => Loc.Instance[$"Customize.{nameof(SystemFeatures)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.WindowSettings20;
    public CustomizeOrder Order { get; init; } = CustomizeOrder.System;
    public ObservableCollection<ICustomizeSetting> Features { get; init; } = [];

    [CustomizeSetting(
        Section = nameof(Sections.Input),
        Icon = SymbolRegular.NumberSymbol24,
        Recommendation = RecommendationState.On
    )]
    public class NumLockOnBoot : BaseCustomizeSetting
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKU\.DEFAULT\Control Panel\Keyboard",
                    Name = "InitialKeyboardIndicators",
                    OnValue = 2,
                    OffValue = 0,
                    DefaultValue = 0,
                },
                new()
                {
                    Path = @"HKCU\Control Panel\Keyboard",
                    Name = "InitialKeyboardIndicators",
                    OnValue = 2,
                    OffValue = 0,
                    DefaultValue = 0,
                },
            ];
    }
}
