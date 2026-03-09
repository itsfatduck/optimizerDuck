using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.ToggleFeatures;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.UI.Views.Pages.ToggleFeatures;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures;

[ToggleFeatureCategory(PageType = typeof(SystemToggleFeaturesCategory))]
public class System : IToggleFeaturesCategory
{
    public string Name => Loc.Instance[$"ToggleFeature.Category.{nameof(System)}"];
    public string Description => Loc.Instance[$"ToggleFeature.Category.{nameof(System)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Desktop24;
    public ToggleFeaturesCategoryOrder Order { get; init; } = ToggleFeaturesCategoryOrder.System;
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];

    public enum Sections
    {
        WindowsUpdate,
        Storage
    }

    [ToggleFeature(Section = nameof(Sections.WindowsUpdate))]
    public class DisableAutomaticWindowsUpdate : BaseToggleFeature
    {
        protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                Name = "NoAutoUpdate",
                OnValue = 1,
                OffValue = 0,
                DefaultValue = 0
            }
        ];
    }

    [ToggleFeature(Section = nameof(Sections.Storage))]
    public class DisableStorageSense : BaseToggleFeature
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
