using System.Collections.ObjectModel;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Features.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Pages.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Features.Categories;

[FeatureCategory(PageType = typeof(DesktopFeatureCategory))]
public class Desktop : IFeatureCategory
{
    private enum Sections
    {
        Icons,
        Behaviors,
    }

    public string Name => Loc.Instance[$"Features.{nameof(Desktop)}.Name"];
    public string Description => Loc.Instance[$"Features.{nameof(Desktop)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.Desktop16;
    public FeatureCategoryOrder Order { get; init; } = FeatureCategoryOrder.Desktop;
    public ObservableCollection<IFeature> Features { get; init; } = [];

    [Feature(Section = nameof(Sections.Icons), Icon = SymbolRegular.Laptop24)]
    public class ShowThisPc : BaseFeature
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path =
                        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                    Name = "{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 0,
                },
            ];
    }

    [Feature(Section = nameof(Sections.Icons), Icon = SymbolRegular.Delete24)]
    public class ShowRecycleBin : BaseFeature
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path =
                        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                    Name = "{645FF040-5081-101B-9F08-00AA002F954E}",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 0,
                },
            ];
    }

    [Feature(Section = nameof(Sections.Icons), Icon = SymbolRegular.Folder24)]
    public class ShowUserFiles : BaseFeature
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path =
                        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                    Name = "{59031a47-3f72-44a7-89c5-5595fe6b30ee}",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 1,
                },
            ];
    }

    [Feature(Section = nameof(Sections.Icons), Icon = SymbolRegular.Desktop24)]
    public class ShowNetwork : BaseFeature
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path =
                        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                    Name = "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 1,
                },
            ];
    }

    [Feature(Section = nameof(Sections.Icons), Icon = SymbolRegular.Grid24)]
    public class ShowControlPanel : BaseFeature
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path =
                        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                    Name = "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 1,
                },
            ];
    }

    [Feature(Section = nameof(Sections.Icons), Icon = SymbolRegular.Desktop24)]
    public class ShowDesktopIcons : BaseFeature
    {
        protected override bool NeedsPostAction => true;

        protected override IEnumerable<RegistryToggle> RegistryToggles =>
            [
                new()
                {
                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    Name = "NoDesktop",
                    OnValue = 0,
                    OffValue = 1,
                    DefaultValue = 0,
                },
            ];
    }

    [Feature(Section = nameof(Sections.Behaviors), Icon = SymbolRegular.ArrowForward24)]
    public class ShortcutArrow : BaseFeature
    {
        private const string Path =
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";

        protected override bool NeedsPostAction => true;

        public override Task<bool> GetStateAsync()
        {
            var value = RegistryService.Read<string>(new RegistryItem(Path, "29"));
            return Task.FromResult(string.Equals(value, "", StringComparison.OrdinalIgnoreCase));
        }

        public override async Task EnableAsync()
        {
            RegistryService.Write(
                new RegistryItem(Path, "29", @"%windir%\System32\shell32.dll,-50")
            );
            if (NeedsPostAction)
                await ExecutePostActionAsync();
        }

        public override async Task DisableAsync()
        {
            RegistryService.DeleteValue(new RegistryItem(Path, "29"));
            if (NeedsPostAction)
                await ExecutePostActionAsync();
        }
    }
}
