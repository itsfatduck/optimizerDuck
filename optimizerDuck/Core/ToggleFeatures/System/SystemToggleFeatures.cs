using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.ToggleFeatures.System;

[ToggleFeatureCategory]
public class System : IToggleFeatureCategory
{
    public string Name { get; init; } = Loc.Instance["ToggleFeature.Category.System.Name"];
    public ToggleFeatureCategoryOrder Order { get; init; } = ToggleFeatureCategoryOrder.System;
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];

    [ToggleFeature(Id = "TF-System-001", Risk = OptimizationRisk.Risky, Type = ToggleFeatureType.Registry)]
    public class DisableAutomaticWindowsUpdate : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
            Name = "NoAutoUpdate",
            OnValue = 1,
            OffValue = 0,
            DefaultValue = 0
        };
    }

    [ToggleFeature(Id = "TF-System-002", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class DisableStorageSense : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense",
            Name = "StorageSenseStatus",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }
}
