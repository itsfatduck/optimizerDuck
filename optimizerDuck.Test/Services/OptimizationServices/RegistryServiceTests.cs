using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.Services.OptimizationServices;
using Xunit;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class RegistryServiceTests : IDisposable
{
    private const string BaseTestKey = @"HKCU\Software\TestOptimizerDuck";
    private readonly ExecutionScope _scope;

    private class DummyOptimization : IOptimization
    {
        public Guid Id { get; } = Guid.NewGuid();
        public OptimizationRisk Risk => OptimizationRisk.Safe;
        public string OptimizationKey => "TestOpt";
        public string Name => "Test";
        public string ShortDescription => "";
        public OptimizationState State { get; set; } = new();

        public Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        ) => Task.FromResult(ApplyResult.True());
    }

    public RegistryServiceTests()
    {
        // Setup execution scope to capture revert steps and logs
        _scope = ExecutionScope.Begin(new DummyOptimization(), NullLogger.Instance);

        // Ensure clean state
        CleanupTestKey();
    }

    public void Dispose()
    {
        CleanupTestKey();
        _scope.Dispose();
    }

    private static void CleanupTestKey()
    {
        try
        {
            using var hkcu = Registry.CurrentUser;
            hkcu.DeleteSubKeyTree(@"Software\TestOptimizerDuck", false);
        }
        catch
        {
            // Ignore if it doesn't exist
        }
    }

    [Fact]
    public void Read_WithInvalidRoot_ReturnsDefault()
    {
        var item = new RegistryItem("BADROOT\\SomePath", "ValueName");
        var result = RegistryService.Read<string>(item);
        Assert.Null(result);
    }

    [Fact]
    public void DeleteValue_WithInvalidRoot_ReturnsFalse()
    {
        var item = new RegistryItem("BADROOT\\SomePath", "ValueName");
        var result = RegistryService.DeleteValue(item);
        Assert.False(result);
    }

    [Fact]
    public void Write_WithNullValue_ReturnsFalse()
    {
        var item = new RegistryItem(BaseTestKey, "TestValue");
        var result = RegistryService.Write(item);
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteValue_RestorePreviousValue_OnRevert()
    {
        var key = $@"{BaseTestKey}\DeleteValueTest";

        RegistryService.Write(new RegistryItem(key, "A", "Hello"));
        RegistryService.DeleteValue(new RegistryItem(key, "A"));

        var step = _scope.ExecutedSteps.Last();

        await step.RevertStep!.ExecuteAsync();

        var val = RegistryService.Read<string>(new RegistryItem(key, "A"));
        Assert.Equal("Hello", val);
    }

    [Fact]
    public async Task DeleteDefaultValue_RestorePreviousValue_OnRevert()
    {
        var key = $@"{BaseTestKey}\DeleteDefaultValueTest";

        RegistryService.Write(new RegistryItem(key, null, "DefaultHello"));
        RegistryService.DeleteValue(new RegistryItem(key, null));

        var step = _scope.ExecutedSteps.Last();

        await step.RevertStep!.ExecuteAsync();

        var value = RegistryService.Read<string>(new RegistryItem(key, null));
        Assert.Equal("DefaultHello", value);
    }

    [Fact]
    public async Task WriteDefaultValue_OverwriteValue_RevertRestoresPreviousValue()
    {
        var key = $@"{BaseTestKey}\WriteDefaultValueTest";

        Assert.True(RegistryService.Write(new RegistryItem(key, null, "OriginalDefault")));
        Assert.True(RegistryService.Write(new RegistryItem(key, null, "UpdatedDefault")));

        var value = RegistryService.Read<string>(new RegistryItem(key, null));
        Assert.Equal("UpdatedDefault", value);

        var step = _scope.ExecutedSteps.Last();
        Assert.True(await step.RevertStep!.ExecuteAsync());

        var restoredValue = RegistryService.Read<string>(new RegistryItem(key, null));
        Assert.Equal("OriginalDefault", restoredValue);
    }

    [Fact]
    public async Task DeleteSubKeyTree_CreatesRecursiveBackupAndRestoresCorrectly()
    {
        // 1. Setup a complex tree structure
        // A
        // |- Value1: "Data1"
        // |- Value2 (Default): "DefaultData"
        // |- B
        //    |- Value3: 42 (DWORD)
        //    |- C
        var keyPathA = $@"{BaseTestKey}\A";
        var keyPathB = $@"{keyPathA}\B";
        var keyPathC = $@"{keyPathB}\C";

        Assert.True(RegistryService.Write(new RegistryItem(keyPathA, "Value1", "Data1")));
        Assert.True(RegistryService.Write(new RegistryItem(keyPathA, null, "DefaultData")));
        Assert.True(
            RegistryService.Write(new RegistryItem(keyPathB, "Value3", 42, RegistryValueKind.DWord))
        );
        Assert.True(RegistryService.CreateSubKey(new RegistryItem(keyPathC)));

        // 2. Perform the deletion of tree A
        Assert.True(RegistryService.DeleteSubKeyTree(new RegistryItem(keyPathA)));

        // Verify keys are gone
        Assert.False(RegistryService.KeyExists(new RegistryItem(keyPathA)));

        // 3. Obtain the revert step generated out of the deleted tree
        var deleteStep = _scope.ExecutedSteps.LastOrDefault(s =>
            s.Name == "Registry" && s.RevertStep != null
        );
        Assert.NotNull(deleteStep);

        var revertStep = deleteStep.RevertStep;
        Assert.NotNull(revertStep);
        Assert.Equal("Registry", revertStep.Type);

        // 4. Act: Execute the Revert Step
        Assert.True(await revertStep.ExecuteAsync());

        // 5. Assert: Verify the tree structure is perfectly restored
        Assert.True(RegistryService.KeyExists(new RegistryItem(keyPathC))); // C exists, implies A and B exist

        // Check values
        var value1 = RegistryService.Read<string>(new RegistryItem(keyPathA, "Value1"));
        Assert.Equal("Data1", value1);

        var defaultVal = RegistryService.Read<string>(new RegistryItem(keyPathA, null));
        Assert.Equal("DefaultData", defaultVal);

        var value3 = RegistryService.Read<int>(new RegistryItem(keyPathB, "Value3"));
        Assert.Equal(42, value3);
    }

    [Fact]
    public async Task CreateSubKey_TracksCreatedKeysAndRemovesIfEmptyOnRevert()
    {
        var keyPathA = $@"{BaseTestKey}\CreateTest_A";
        var keyPathB = $@"{keyPathA}\B";

        // Create the deep subkey
        Assert.True(RegistryService.CreateSubKey(new RegistryItem(keyPathB)));
        Assert.True(RegistryService.KeyExists(new RegistryItem(keyPathB)));

        // Obtain revert step
        var createStep = _scope.ExecutedSteps.LastOrDefault(s =>
            s.Name == "Registry" && s.RevertStep != null
        );
        Assert.NotNull(createStep);

        // Execute revert
        Assert.True(await createStep.RevertStep!.ExecuteAsync());

        // Verify that B and A were cleaned up since they were empty
        Assert.False(RegistryService.KeyExists(new RegistryItem(keyPathB)));
        Assert.False(RegistryService.KeyExists(new RegistryItem(keyPathA)));
    }
}
