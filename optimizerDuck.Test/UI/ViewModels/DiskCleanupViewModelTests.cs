using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using optimizerDuck.Domain.Optimizations.Models.Cleanup;
using optimizerDuck.Services.UI;
using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Test.UI.ViewModels;

public class DiskCleanupViewModelTests
{
    private static DiskCleanupViewModel NewViewModel(
        StubSnackbarService? snackbar = null,
        StubContentDialogService? dialog = null
    ) =>
        new(
            new DiskCleanupService(NullLogger<DiskCleanupService>.Instance),
            snackbar ?? new StubSnackbarService(),
            dialog ?? new StubContentDialogService(),
            NullLogger<DiskCleanupViewModel>.Instance
        );

    [Fact]
    public async Task CleanItemCommand_IgnoresNullZeroSizeAndBusyStates()
    {
        var snackbar = new StubSnackbarService();
        var vm = NewViewModel(snackbar: snackbar);
        var missingDir = Path.Combine(
            Path.GetTempPath(),
            $"optimizerDuck_VmMissing_{Guid.NewGuid():N}"
        );
        var item = new CleanupItem
        {
            Id = "TempFiles",
            NameKey = "Temp Files",
            DescriptionKey = "Temp Description",
            Path = missingDir,
            Icon = SymbolRegular.Document24,
            SizeBytes = 1024,
            IsSelected = true,
            IsScanned = true,
        };

        await vm.CleanItemCommand.ExecuteAsync(null);
        Assert.False(vm.IsCleaning);
        Assert.False(vm.IsScanning);

        vm.IsCleaning = true;
        await vm.CleanItemCommand.ExecuteAsync(item);
        await vm.ScanCommand.ExecuteAsync(null);
        await vm.RefreshItemCommand.ExecuteAsync(item);
        Assert.True(item.IsSelected);
        Assert.Empty(snackbar.Shown);

        vm.IsCleaning = false;
        vm.IsScanning = true;
        await vm.CleanItemCommand.ExecuteAsync(item);
        await vm.RefreshItemCommand.ExecuteAsync(item);
        Assert.True(item.IsSelected);
        Assert.Empty(snackbar.Shown);

        vm.IsScanning = false;
        item.SizeBytes = 0;
        await vm.CleanItemCommand.ExecuteAsync(item);
        Assert.Empty(snackbar.Shown);
    }

    [Fact]
    public void IsCleaningAndIsScanning_NotifyCanCleanChanges()
    {
        var vm = NewViewModel();
        vm.CleanupItems.Add(
            new CleanupItem
            {
                Id = "TempFiles",
                NameKey = "Temp Files",
                DescriptionKey = "Temp Description",
                Path = string.Empty,
                Icon = SymbolRegular.Document24,
                SizeBytes = 512,
                IsSelected = true,
                IsScanned = true,
            }
        );

        Assert.True(vm.CanClean);

        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        vm.IsCleaning = true;
        Assert.False(vm.CanClean);
        Assert.Contains(nameof(DiskCleanupViewModel.CanClean), notified);

        notified.Clear();
        vm.IsCleaning = false;
        Assert.True(vm.CanClean);
        Assert.Contains(nameof(DiskCleanupViewModel.CanClean), notified);

        notified.Clear();
        vm.IsScanning = true;
        Assert.False(vm.CanClean);
        Assert.Contains(nameof(DiskCleanupViewModel.CanClean), notified);

        notified.Clear();
        vm.IsScanning = false;
        Assert.True(vm.CanClean);
        Assert.Contains(nameof(DiskCleanupViewModel.CanClean), notified);
    }

    [Fact]
    public async Task CleanItemCommand_TogglesCleaningAndScanningStatesAndClearsSelection()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"optimizerDuck_CleanItemVm_{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(tempRoot);
        var tempFile = Path.Combine(tempRoot, "stale.tmp");
        await File.WriteAllTextAsync(tempFile, "123456", TestContext.Current.CancellationToken);

        try
        {
            await RunInStaThreadAsync(async () =>
            {
                var snackbar = new StubSnackbarService();
                var vm = NewViewModel(snackbar: snackbar);
                var item = new CleanupItem
                {
                    Id = "TempFiles",
                    NameKey = "Temp Files",
                    DescriptionKey = "Temp Description",
                    Path = tempRoot,
                    Icon = SymbolRegular.Document24,
                    SizeBytes = 6,
                    FileCount = 1,
                    IsSelected = true,
                    IsScanned = true,
                };
                vm.CleanupItems.Add(item);

                var cleaningStates = new List<bool>();
                var scanningStates = new List<bool>();
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(DiskCleanupViewModel.IsCleaning))
                        cleaningStates.Add(vm.IsCleaning);
                    else if (e.PropertyName == nameof(DiskCleanupViewModel.IsScanning))
                        scanningStates.Add(vm.IsScanning);
                };

                await vm.CleanItemCommand.ExecuteAsync(item);

                Assert.False(File.Exists(tempFile));
                Assert.False(item.IsSelected);
                Assert.False(vm.IsCleaning);
                Assert.False(vm.IsScanning);
                Assert.Single(snackbar.Shown);
                Assert.Equal([true, false], cleaningStates);
                Assert.Equal([true, false], scanningStates);
            });
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch { }
            }
        }
    }

    private static Task RunInStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(dispatcher)
            );
            var frame = new DispatcherFrame();

            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                finally
                {
                    frame.Continue = false;
                }
            });

            Dispatcher.PushFrame(frame);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    private sealed class StubSnackbarService : ISnackbarService
    {
        public List<(string Title, string Message)> Shown { get; } = [];
        public TimeSpan DefaultTimeOut { get; set; } = TimeSpan.FromSeconds(3);

        public SnackbarPresenter? GetSnackbarPresenter() => null;

        public void SetSnackbarPresenter(SnackbarPresenter presenter) { }

        public void Show(
            string title,
            string message,
            ControlAppearance appearance,
            IconElement? icon,
            TimeSpan timeout
        ) => Shown.Add((title, message));
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
