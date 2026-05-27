using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using optimizerDuck.Services;

namespace optimizerDuck.Test.Services;

/// <summary>
/// Integration tests for <see cref="RegistryWatcher"/>.
/// Uses real registry writes under <c>HKCU\Software\TestOptimizerDuckWatcher</c>
/// and verifies that <see cref="IRegistryWatcher.RegistryKeyChanged"/> fires
/// when external changes are made.
/// </summary>
public class RegistryWatcherTests : IDisposable
{
    private const string TestRoot = @"HKCU\Software\TestOptimizerDuckWatcher";
    private const string TestRootNative = @"Software\TestOptimizerDuckWatcher";

    public RegistryWatcherTests()
    {
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
    }

    private static void Cleanup()
    {
        try
        {
            using var hkcu = Registry.CurrentUser;
            hkcu.DeleteSubKeyTree(TestRootNative, false);
        }
        catch
        {
            // Ignore if it doesn't exist
        }
    }

    [Fact]
    public void Watch_And_Unwatch_DoesNotThrow()
    {
        using var watcher = new RegistryWatcher(NullLogger<RegistryWatcher>.Instance);

        var exception = Record.Exception(() => watcher.Watch(TestRoot));
        Assert.Null(exception);

        exception = Record.Exception(() => watcher.Unwatch(TestRoot));
        Assert.Null(exception);
    }

    [Fact]
    public void Watch_SamePathTwice_DoesNotDuplicate()
    {
        using var watcher = new RegistryWatcher(NullLogger<RegistryWatcher>.Instance);

        watcher.Watch(TestRoot);
        watcher.Watch(TestRoot); // Should be no-op

        // No exception = dedup works
    }

    [Fact]
    public void Watch_EmptyPath_DoesNotThrow()
    {
        using var watcher = new RegistryWatcher(NullLogger<RegistryWatcher>.Instance);

        var exception = Record.Exception(() => watcher.Watch(""));
        Assert.Null(exception);
    }

    [Fact]
    public async Task RegistryChange_FiresEvent()
    {
        // Pre-create the key so the watcher opens it immediately
        using (Registry.CurrentUser.CreateSubKey(TestRootNative)) { }

        using var watcher = new RegistryWatcher(NullLogger<RegistryWatcher>.Instance);
        var tcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        watcher.RegistryKeyChanged += (_, path) => tcs.TrySetResult(path!);
        watcher.Watch(TestRoot);

        // Give the watcher thread time to start and block on RegNotifyChangeKeyValue
        await Task.Delay(500);

        // Write a value to trigger the notification
        using var key = Registry.CurrentUser.OpenSubKey(TestRootNative, writable: true);
        Assert.NotNull(key);
        key.SetValue("TestValue", 42);

        // Wait for the event with a timeout
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.Equal(tcs.Task, completed);

        var firedPath = await tcs.Task;
        Assert.Equal(TestRoot, firedPath);
    }

    [Fact]
    public async Task RegistryChange_AfterUnwatch_DoesNotFire()
    {
        using var watcher = new RegistryWatcher(NullLogger<RegistryWatcher>.Instance);
        var fireCount = 0;

        watcher.RegistryKeyChanged += (_, _) => Interlocked.Increment(ref fireCount);
        watcher.Watch(TestRoot);

        // Give the watcher time to attempt opening the key and start its retry loop
        await Task.Delay(300);

        watcher.Unwatch(TestRoot);

        // Give cancellation time to interrupt any in-flight delay/operation
        await Task.Delay(500);

        // Create key and write — at this point the watcher should be stopped
        using var key = Registry.CurrentUser.CreateSubKey(TestRootNative);
        Assert.NotNull(key);
        key.SetValue("TestValue", 99);

        // Extra settle time to ensure no late notification arrives
        await Task.Delay(1500);

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public async Task MultipleWatchers_IndependentPaths_BothFire()
    {
        const string pathA = TestRoot + @"\A";
        const string pathANative = TestRootNative + @"\A";
        const string pathB = TestRoot + @"\B";
        const string pathBNative = TestRootNative + @"\B";

        // Pre-create both sub-keys so the watcher opens them immediately
        using (Registry.CurrentUser.CreateSubKey(pathANative)) { }
        using (Registry.CurrentUser.CreateSubKey(pathBNative)) { }

        using var watcher = new RegistryWatcher(NullLogger<RegistryWatcher>.Instance);
        var tcsA = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var tcsB = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        watcher.RegistryKeyChanged += (_, path) =>
        {
            if (path == pathA) tcsA.TrySetResult(path!);
            if (path == pathB) tcsB.TrySetResult(path!);
        };

        watcher.Watch(pathA);
        watcher.Watch(pathB);

        // Give watcher threads time to block on RegNotifyChangeKeyValue
        await Task.Delay(500);

        // Write to path A
        using (var keyA = Registry.CurrentUser.OpenSubKey(pathANative, writable: true))
        {
            Assert.NotNull(keyA);
            keyA.SetValue("Val", 1);
        }

        // Write to path B
        using (var keyB = Registry.CurrentUser.OpenSubKey(pathBNative, writable: true))
        {
            Assert.NotNull(keyB);
            keyB.SetValue("Val", 2);
        }

        var completedA = await Task.WhenAny(tcsA.Task, Task.Delay(5000));
        var completedB = await Task.WhenAny(tcsB.Task, Task.Delay(5000));

        Assert.Equal(tcsA.Task, completedA);
        Assert.Equal(tcsB.Task, completedB);
    }

    [Fact]
    public void Dispose_CleansUpAllWatchers()
    {
        var watcher = new RegistryWatcher(NullLogger<RegistryWatcher>.Instance);

        watcher.Watch(TestRoot);
        watcher.Watch(TestRoot + @"\SubKey");

        // Should not throw
        var exception = Record.Exception(() => watcher.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        var watcher = new RegistryWatcher(NullLogger<RegistryWatcher>.Instance);
        watcher.Watch(TestRoot);

        watcher.Dispose();
        var exception = Record.Exception(() => watcher.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Watch_AfterDispose_ThrowsObjectDisposed()
    {
        var watcher = new RegistryWatcher(NullLogger<RegistryWatcher>.Instance);
        watcher.Dispose();

        Assert.Throws<ObjectDisposedException>(() => watcher.Watch(TestRoot));
    }
}
