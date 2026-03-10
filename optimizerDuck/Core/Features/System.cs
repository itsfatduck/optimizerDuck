using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Features;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.Views.Pages.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Features;

[FeatureCategory(PageType = typeof(SystemFeatureCategory))]
public class System : IFeatureCategory
{
    private enum Sections
    {
        Storage
    }

    public string Name => Loc.Instance[$"Features.{nameof(System)}"];
    public string Description => Loc.Instance[$"Features.{nameof(System)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Desktop24;
    public FeatureCategoryOrder Order { get; init; } = FeatureCategoryOrder.System;
    public ObservableCollection<IFeature> Features { get; init; } = [];


    [Feature(Section = nameof(Sections.Storage), Icon = SymbolRegular.HardDrive20)]
    public class StorageSense : BaseFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense",
                Name = "StorageSenseStatus",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 1
            }
        ];
    }
}