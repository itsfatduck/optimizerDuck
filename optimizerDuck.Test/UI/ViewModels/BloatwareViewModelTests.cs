using System.Windows.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Domain.Optimizations.Models.Bloatware;
using optimizerDuck.Services.UI;
using optimizerDuck.Test.TestDoubles;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Test.UI.ViewModels;

public class BloatwareViewModelTests
{
    private static AppXPackage MakePackage(string name, AppRisk risk, bool isSelected = false) =>
        new()
        {
            Name = name,
            PackageFullName = $"{name}_1.0.0.0_x64__8wekyb3d8bbwe",
            Publisher = "CN=Test",
            Version = "1.0.0.0",
            InstallLocation = string.Empty,
            Risk = risk,
            IsSelected = isSelected,
        };

    private static BloatwareViewModel NewViewModel(StubBloatwareService service) =>
        new(service, new StubContentDialogService(), NullLogger<BloatwareViewModel>.Instance);

    [Fact]
    public async Task ApplyFilter_ResetsAndRestoresSelectedCountAndCanExecute()
    {
        var safePkg = MakePackage("SafeApp", AppRisk.Safe);
        var cautionPkg = MakePackage("CautionApp", AppRisk.Caution);
        var service = new StubBloatwareService([safePkg, cautionPkg]);
        var vm = NewViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        safePkg.IsSelected = true;

        Assert.Equal(1, vm.SelectedCount);
        Assert.True(vm.HasSelectedItems);
        Assert.True(vm.RemoveSelectedCommand.CanExecute(null));

        // Filter to Caution only — SafeApp leaves the visible collection.
        vm.SelectedRiskFilterIndex = 2;

        Assert.Single(vm.AppxPackages);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.HasSelectedItems);
        Assert.False(vm.RemoveSelectedCommand.CanExecute(null));

        // Switch back to All — SafeApp is visible and selected again.
        vm.SelectedRiskFilterIndex = 0;

        Assert.Equal(2, vm.AppxPackages.Count);
        Assert.Equal(1, vm.SelectedCount);
        Assert.True(vm.HasSelectedItems);
        Assert.True(vm.RemoveSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Refresh_DetachesOldSelectionListenersAndResetsCount()
    {
        var oldPkg = MakePackage("OldApp", AppRisk.Safe, isSelected: true);
        var newPkg = MakePackage("NewApp", AppRisk.Safe, isSelected: false);
        var service = new StubBloatwareService([oldPkg]);
        var vm = NewViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.SelectedCount);

        service.Packages = [newPkg];
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.HasSelectedItems);

        // Mutating a cleared item must not corrupt the refreshed view model's count.
        oldPkg.IsSelected = false;

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.HasSelectedItems);
    }

    private sealed class StubBloatwareService(List<AppXPackage> packages)
        : BloatwareService(
            NullLogger<BloatwareService>.Instance,
            new StubOptionsMonitor(),
            TestShell.New()
        )
    {
        public List<AppXPackage> Packages { get; set; } = packages;

        public override Task<List<AppXPackage>> GetAppXPackagesAsync() => Task.FromResult(Packages);
    }

    private sealed class StubOptionsMonitor : IOptionsMonitor<AppSettings>
    {
        public AppSettings CurrentValue { get; } = new();

        public AppSettings Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<AppSettings, string?> listener) => null;
    }

    private sealed class StubContentDialogService : IContentDialogService
    {
        public Task<ContentDialogResult> ShowAsync(
            ContentDialog dialog,
            CancellationToken cancellationToken
        ) => Task.FromResult(ContentDialogResult.None);

        public ContentPresenter? GetDialogHost() => null;

        public ContentDialogHost? GetDialogHostEx() => null;

        public void SetDialogHost(ContentDialogHost dialogHost) { }

        public void SetDialogHost(ContentPresenter contentPresenter) { }

        public ContentPresenter? GetContentPresenter() => null;

        public void SetContentPresenter(ContentPresenter contentPresenter) { }
    }
}
