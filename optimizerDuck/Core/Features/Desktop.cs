using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Features;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Views.Pages.Features;
using Wpf.Ui.Controls;

namespace optimizerDuck.Core.Features;

[FeatureCategory(PageType = typeof(DesktopFeatureCategory))]
public class Desktop : IFeatureCategory
{
    private enum Sections
    {
        Icons,
        Behaviors
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
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                Name = "{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 0
            }
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
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                Name = "{645FF040-5081-101B-9F08-00AA002F954E}",
                OnValue = 0,
                OffValue = 1,
                DefaultValue = 0
            }
        ];
    }


    [Feature(Section = nameof(Sections.Behaviors), Icon = SymbolRegular.ArrowForward24)]
    public class RemoveShortcutArrow : BaseFeature
    {
        private const string Path = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";

        protected override bool NeedsPostAction => true;

        public override Task<bool> GetStateAsync()
        {
            var value = RegistryService.Read<string>(new RegistryItem(Path, "29"));
            return Task.FromResult(value?.ToString() == @"%windir%\System32\shell32.dll,-50");
        }

        public override async Task EnableAsync()
        {
            RegistryService.Write(new RegistryItem(Path, "29", @"%windir%\System32\shell32.dll,-50", Microsoft.Win32.RegistryValueKind.String));
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
