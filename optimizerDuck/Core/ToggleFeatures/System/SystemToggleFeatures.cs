using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures.System;

public class DisableAutomaticWindowsUpdate : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableAutomaticWindowsUpdate.Name";
    public override string Description => "ToggleFeature.DisableAutomaticWindowsUpdate.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Risky;
    public override SymbolRegular Icon => SymbolRegular.ArrowSync24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
        Name = "NoAutoUpdate",
        OnValue = 1,
        OffValue = 0,
        DefaultValue = 0
    };
}

public class DisableStorageSense : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableStorageSense.Name";
    public override string Description => "ToggleFeature.DisableStorageSense.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.Storage24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense",
        Name = "StorageSenseStatus",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}
