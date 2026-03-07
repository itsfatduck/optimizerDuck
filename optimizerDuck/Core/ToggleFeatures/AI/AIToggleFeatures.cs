using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Core.ToggleFeatures.AI;

[ToggleFeatureCategory]
public class AI : IToggleFeatureCategory
{
    public string Name { get; init; } = Loc.Instance["ToggleFeature.Category.AI.Name"];
    public ToggleFeatureCategoryOrder Order { get; init; } = ToggleFeatureCategoryOrder.AI;
    public ObservableCollection<IToggleFeature> Features { get; init; } = [];

    [ToggleFeature(Id = "TF-AI-001", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class DisableWindowsCopilot : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Policies\Microsoft\Windows\Windows Copilot",
            Name = "TurnOffWindowsCopilot",
            OnValue = 1,
            OffValue = 0,
            DefaultValue = 0
        };
    }

    [ToggleFeature(Id = "TF-AI-002", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class DisableBingInWindowsSearch : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",
            Name = "BingSearchEnabled",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }

    [ToggleFeature(Id = "TF-AI-003", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class DisableSuggestionsInStart : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
            Name = "SubscribedContent-338389Enabled",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }

    [ToggleFeature(Id = "TF-AI-004", Risk = OptimizationRisk.Safe, Type = ToggleFeatureType.Registry)]
    public class DisableTailoredExperiences : RegistryToggleFeature
    {
        public RegistryToggle Toggle { get; } = new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy",
            Name = "TailoredExperiencesWithDiagnosticDataEnabled",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1
        };
    }
}
