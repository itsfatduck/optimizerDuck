using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.ToggleFeatures.Privacy;

[ToggleFeatureCategory]
public class Privacy : IToggleFeatureCategory
{
    public string Name { get; init; } = Loc.Instance["ToggleFeature.Category.Privacy.Name"];
    public ToggleFeatureCategoryOrder Order { get; init; } = ToggleFeatureCategoryOrder.Privacy;
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];

    [ToggleFeature(Id = "TF-Privacy-001", Risk = OptimizationRisk.Moderate, Type = ToggleFeatureType.Registry)]
    public class DisableTelemetry : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            Name = "AllowTelemetry",
            OnValue = 0,
            OffValue = 3,
            DefaultValue = 3
        };
    }

    [ToggleFeature(Id = "TF-Privacy-002", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class DisableDiagnosticData : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack",
            Name = "ShowedToastAtLevel",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }

    [ToggleFeature(Id = "TF-Privacy-003", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class DisableFeedbackNotifications : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\FeedbackHub\Privacy",
            Name = "FeedbackStorageAllowed",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }
}
