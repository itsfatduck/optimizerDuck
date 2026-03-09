using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using optimizerDuck.Services.Managers;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures.System;

[ToggleFeatureCategory(PageType = typeof(SystemToggleFeaturesCategory))]
public class System : IToggleFeatureCategory
{
    public string Name { get; init; } = Loc.Instance["ToggleFeature.Category.System.Name"];
    public string Description { get; init; } = Loc.Instance["ToggleFeature.Category.System.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Desktop24;
    public ToggleFeatureCategoryOrder Order { get; init; } = ToggleFeatureCategoryOrder.System;
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];

    [ToggleFeature]
    public class DisableAutomaticWindowsUpdate : BaseToggleFeature
    {
        public override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new RegistryToggle
            {
                Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                Name = "NoAutoUpdate",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 0
            }
        ];
    }

    [ToggleFeature]
    public class DisableStorageSense : BaseToggleFeature
    {
        public override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new RegistryToggle
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
