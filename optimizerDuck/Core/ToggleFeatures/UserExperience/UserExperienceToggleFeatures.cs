using optimizerDuck.Core.Models.UI;
using optimizerDuck.Core.ToggleFeatures;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.ToggleFeatures.UserExperience;

public class DisableTaskbarNewsAndInterests : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableTaskbarNewsAndInterests.Name";
    public override string Description => "ToggleFeature.DisableTaskbarNewsAndInterests.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.News24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKLM\SOFTWARE\Policies\Microsoft\Dsh",
        Name = "AllowNewsAndInterests",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}

public class EnableDarkMode : BaseToggleFeature
{
    public override string Name => "ToggleFeature.EnableDarkMode.Name";
    public override string Description => "ToggleFeature.EnableDarkMode.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.WeatherMoon24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        Name = "AppsUseLightTheme",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}

public class DisableVisualEffects : BaseToggleFeature
{
    public override string Name => "ToggleFeature.DisableVisualEffects.Name";
    public override string Description => "ToggleFeature.DisableVisualEffects.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.PaintBrush24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
        Name = "TaskbarAnimations",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}

public class ShowSecondsInSystemClock : BaseToggleFeature
{
    public override string Name => "ToggleFeature.ShowSecondsInSystemClock.Name";
    public override string Description => "ToggleFeature.ShowSecondsInSystemClock.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.Clock24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
        Name = "ShowSecondsInSystemClock",
        OnValue = 1,
        OffValue = 0,
        DefaultValue = 0
    };
}

public class EnableClassicContextMenu : BaseToggleFeature
{
    public override string Name => "ToggleFeature.EnableClassicContextMenu.Name";
    public override string Description => "ToggleFeature.EnableClassicContextMenu.Description";
    public override OptimizationRisk Risk => OptimizationRisk.Safe;
    public override SymbolRegular Icon => SymbolRegular.List24;

    protected new RegistryToggle Toggle { get; } = new()
    {
        Path = @"HKCU\SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
        Name = "",
        OnValue = 0,
        OffValue = 1,
        DefaultValue = 1
    };
}
