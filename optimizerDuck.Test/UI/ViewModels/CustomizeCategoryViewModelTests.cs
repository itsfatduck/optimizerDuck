using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Customize.Models;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.System;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace optimizerDuck.Test.UI.ViewModels;

public sealed class CustomizeCategoryViewModelTests
{
    private sealed class FakeCategory : ICustomizeCategory
    {
        public string Name => "Test";
        public string Description => "Test";
        public SymbolRegular Icon { get; init; } = SymbolRegular.Settings24;
        public CustomizeOrder Order { get; init; }
        public ObservableCollection<ICustomizeSetting> Features { get; init; } = [];
    }

    private sealed class FakeRegistryWatcher : IRegistryWatcher
    {
        public event EventHandler<string>? RegistryKeyChanged;

        public void Watch(string registryPath) { }

        public void Unwatch(string registryPath) { }

        public void Dispose() { }
    }

    [Fact]
    public void SettingCountNotifiesHeader()
    {
        var vm = new CustomizeCategoryViewModel(
            new FakeCategory(),
            NullLoggerFactory.Instance,
            new FakeRegistryWatcher(),
            new SystemInfoService(NullLogger<SystemInfoService>.Instance)
        );

        var notified = new List<string>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName!);

        vm.UnsupportedSettingsCount = 1;

        Assert.Contains(nameof(CustomizeCategoryViewModel.UnsupportedSettingsHeader), notified);
        Assert.Contains("(1)", vm.UnsupportedSettingsHeader);
    }
}
