using System.Collections.ObjectModel;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.Optimization.Providers;
using optimizerDuck.UI.Pages.Customize;
using Wpf.Ui.Controls;

namespace optimizerDuck.Domain.Customize.Categories;

[CustomizeCategory(PageType = typeof(DesktopFeatureCategory))]
public class Desktop : ICustomizeCategory
{
    private enum Sections
    {
        Icons,
        Behaviors,
    }

    public string Name => Loc.Instance[$"Customize.{nameof(Desktop)}.Name"];
    public string Description => Loc.Instance[$"Customize.{nameof(Desktop)}.Description"];
    public SymbolRegular Icon { get; init; } = SymbolRegular.ViewDesktop24;
    public CustomizeOrder Order { get; init; } = CustomizeOrder.Desktop;
    public ObservableCollection<ICustomizeSetting> Features { get; init; } = [];

    [CustomizeSetting(Section = nameof(Sections.Icons), Icon = SymbolRegular.Laptop24)]
    public class ShowThisPc : BaseCustomizeSetting
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

    [CustomizeSetting(Section = nameof(Sections.Icons), Icon = SymbolRegular.Delete24)]
    public class ShowRecycleBin : BaseCustomizeSetting
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

    [CustomizeSetting(Section = nameof(Sections.Icons), Icon = SymbolRegular.Folder24)]
    public class ShowUserFiles : BaseCustomizeSetting
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

    [CustomizeSetting(Section = nameof(Sections.Icons), Icon = SymbolRegular.Desktop24)]
    public class ShowNetwork : BaseCustomizeSetting
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

    [CustomizeSetting(Section = nameof(Sections.Icons), Icon = SymbolRegular.Grid24)]
    public class ShowControlPanel : BaseCustomizeSetting
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

    [CustomizeSetting(Section = nameof(Sections.Icons), Icon = SymbolRegular.Desktop24)]
    public class ShowDesktopIcons : BaseCustomizeSetting
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

    [CustomizeSetting(Section = nameof(Sections.Behaviors), Icon = SymbolRegular.ArrowForward24)]
    public class ShortcutArrow : BaseCustomizeSetting
    {
        private const string Path =
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";

        private readonly string[] HiddenShortcutValues =
        [
            @"%windir%\System32\shell32.dll,-50",
            @"%windir%\System32\shell32.dll,50",
        ];

        private bool IsHiddenShortcutOverlay(string value)
        {
            if (
                HiddenShortcutValues.Any(v =>
                    string.Equals(value, v, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return true;
            }

            var fileName = System.IO.Path.GetFileName(value);

            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            return fileName.Equals("blank.ico", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("transparent.ico", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("blankicon.ico", StringComparison.OrdinalIgnoreCase);
        }

        public override Task<bool> GetStateAsync()
        {
            var value = RegistryService.Read<string>(new RegistryItem(Path, "29"));

            // value doesn't exist = default Windows behavior
            // shortcut arrow is visible
            if (string.IsNullOrWhiteSpace(value))
                return Task.FromResult(true);

            var isHidden = IsHiddenShortcutOverlay(value);

            return Task.FromResult(!isHidden);
        }

        public override async Task ApplyAsync(object? value)
        {
            var isOn = value is bool b && b;

            if (isOn)
            {
                // restore default Windows behavior by deleting the value
                RegistryService.DeleteValue(new RegistryItem(Path, "29"));
            }
            else
            {
                // extract the blank icon to the app resources folder, then set the registry value to point to it
                var outputPath = System.IO.Path.Combine(
                    Shared.AssetsDirectory,
                    nameof(Desktop),
                    "blank.ico"
                );
                EmbeddedResourceHelper.TryExtract("Icons.blank.ico", outputPath);

                // set the registry value to point to the blank icon, which effectively hides the shortcut arrow overlay
                RegistryService.Write(new RegistryItem(Path, "29", outputPath));
            }

            await ExecutePostActionAsync();
        }
    }
}
