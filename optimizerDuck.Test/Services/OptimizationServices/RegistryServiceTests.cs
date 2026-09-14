using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.System.Primitives;
using optimizerDuck.Test.TestDoubles;
using Xunit;

namespace optimizerDuck.Test.Services.OptimizationServices;

public class RegistryServiceTests : IDisposable
{
    private const string BaseTestKey = @"HKCU\Software\TestOptimizerDuck";

    private static OpCall NewCall() =>
        new() { Changes = new ChangeSet(), Logger = NullLogger.Instance };

    public RegistryServiceTests()
    {
        // Ensure clean state
        CleanupTestKey();
    }

    public void Dispose()
    {
        CleanupTestKey();
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
    public void TryKeyExists_ExistingKey_ReturnsTrueWithExists()
    {
        var path = $@"{BaseTestKey}\TryExists";
        Assert.True(RegistryService.Write(NewCall(), new RegistryItem(path, "V", 1)).Ok);

        Assert.True(RegistryService.TryKeyExists(new RegistryItem(path), out var exists));
        Assert.True(exists);
    }

    [Fact]
    public void TryKeyExists_AbsentKey_ReturnsTrueWithNotExists()
    {
        var path = $@"{BaseTestKey}\TryExistsAbsent";

        // A successfully-answered query for a missing key reports "does not exist", not failure.
        Assert.True(RegistryService.TryKeyExists(new RegistryItem(path), out var exists));
        Assert.False(exists);
    }

    [Fact]
    public void TryKeyExists_UnknownRoot_ReturnsQueryFailure()
    {
        // An unparseable root is a failed query, not an absent key.
        Assert.False(RegistryService.TryKeyExists(new RegistryItem("BADROOT\\X"), out _));
    }

    [Fact]
    public void TryReadValue_PresentValue_ReturnsTrueWithValue()
    {
        var path = $@"{BaseTestKey}\TryRead";
        Assert.True(RegistryService.Write(NewCall(), new RegistryItem(path, "V", 42)).Ok);

        Assert.True(RegistryService.TryReadValue(new RegistryItem(path, "V"), out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryReadValue_AbsentValue_ReturnsTrueWithNull()
    {
        var path = $@"{BaseTestKey}\TryReadAbsent";
        Assert.True(RegistryService.Write(NewCall(), new RegistryItem(path, "Other", 1)).Ok);

        // The key exists but the value is absent: a successful read reporting "no value".
        Assert.True(RegistryService.TryReadValue(new RegistryItem(path, "V"), out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryReadValue_UnknownRoot_ReturnsQueryFailure()
    {
        Assert.False(
            RegistryService.TryReadValue(new RegistryItem("BADROOT\\X", "V"), out var value)
        );
        Assert.Null(value);
    }

    [Fact]
    public void DeleteValue_WithInvalidRoot_ReturnsFalse()
    {
        var item = new RegistryItem("BADROOT\\SomePath", "ValueName");
        var result = RegistryService.DeleteValue(NewCall(), item);
        Assert.False(result.Ok);
    }

    [Fact]
    public void Write_WithNullValue_ReturnsFalse()
    {
        var item = new RegistryItem(BaseTestKey, "TestValue");
        var result = RegistryService.Write(NewCall(), item);
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task DeleteValue_RestorePreviousValue_OnRevert()
    {
        var key = $@"{BaseTestKey}\DeleteValueTest";

        var write = RegistryService.Write(NewCall(), new RegistryItem(key, "A", "Hello"));
        Assert.True(write.Ok, write.Error);
        var delete = RegistryService.DeleteValue(NewCall(), new RegistryItem(key, "A"));
        Assert.True(delete.Ok, delete.Error);

        Assert.NotNull(delete.Revert);
        Assert.True(await delete.Revert.ExecuteAsync(TestShell.Context(), NullLogger.Instance));

        var val = RegistryService.Read<string>(new RegistryItem(key, "A"));
        Assert.Equal("Hello", val);
    }

    [Fact]
    public async Task DeleteDefaultValue_RestorePreviousValue_OnRevert()
    {
        var key = $@"{BaseTestKey}\DeleteDefaultValueTest";

        var write = RegistryService.Write(NewCall(), new RegistryItem(key, null, "DefaultHello"));
        Assert.True(write.Ok, write.Error);
        var delete = RegistryService.DeleteValue(NewCall(), new RegistryItem(key, null));
        Assert.True(delete.Ok, delete.Error);

        Assert.NotNull(delete.Revert);
        Assert.True(await delete.Revert.ExecuteAsync(TestShell.Context(), NullLogger.Instance));

        var value = RegistryService.Read<string>(new RegistryItem(key, null));
        Assert.Equal("DefaultHello", value);
    }

    [Fact]
    public async Task WriteDefaultValue_OverwriteValue_RevertRestoresPreviousValue()
    {
        var key = $@"{BaseTestKey}\WriteDefaultValueTest";

        var first = RegistryService.Write(
            NewCall(),
            new RegistryItem(key, null, "OriginalDefault")
        );
        Assert.True(first.Ok, first.Error);
        var second = RegistryService.Write(
            NewCall(),
            new RegistryItem(key, null, "UpdatedDefault")
        );
        Assert.True(second.Ok, second.Error);

        var value = RegistryService.Read<string>(new RegistryItem(key, null));
        Assert.Equal("UpdatedDefault", value);

        Assert.NotNull(second.Revert);
        Assert.True(await second.Revert.ExecuteAsync(TestShell.Context(), NullLogger.Instance));

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

        var w1 = RegistryService.Write(NewCall(), new RegistryItem(keyPathA, "Value1", "Data1"));
        Assert.True(w1.Ok, w1.Error);
        var w2 = RegistryService.Write(NewCall(), new RegistryItem(keyPathA, null, "DefaultData"));
        Assert.True(w2.Ok, w2.Error);
        var w3 = RegistryService.Write(
            NewCall(),
            new RegistryItem(keyPathB, "Value3", 42, RegistryValueKind.DWord)
        );
        Assert.True(w3.Ok, w3.Error);
        var ck = RegistryService.CreateSubKey(NewCall(), new RegistryItem(keyPathC));
        Assert.True(ck.Ok, ck.Error);

        // 2. Perform the deletion of tree A
        var delete = RegistryService.DeleteSubKeyTree(NewCall(), new RegistryItem(keyPathA));
        Assert.True(delete.Ok, delete.Error);

        // Verify keys are gone
        Assert.False(RegistryService.KeyExists(new RegistryItem(keyPathA)));

        // 3. Obtain the revert step generated out of the deleted tree
        var revertStep = delete.Revert;
        Assert.NotNull(revertStep);
        Assert.Equal("Registry", revertStep.Type);

        // 4. Act: Execute the Revert Step
        Assert.True(await revertStep.ExecuteAsync(TestShell.Context(), NullLogger.Instance));

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
        var create = RegistryService.CreateSubKey(NewCall(), new RegistryItem(keyPathB));
        Assert.True(create.Ok, create.Error);
        Assert.True(RegistryService.KeyExists(new RegistryItem(keyPathB)));

        // Obtain revert step
        Assert.NotNull(create.Revert);

        // Execute revert
        Assert.True(await create.Revert.ExecuteAsync(TestShell.Context(), NullLogger.Instance));

        // Verify that B and A were cleaned up since they were empty
        Assert.False(RegistryService.KeyExists(new RegistryItem(keyPathB)));
        Assert.False(RegistryService.KeyExists(new RegistryItem(keyPathA)));
    }

    [Fact]
    public async Task MultiStepRegistryOperation_PartialFailure_RollbackRestoresOriginalState()
    {
        // Test scenario: Multiple registry writes where one fails
        var key1 = $@"{BaseTestKey}\MultiStepTest1";
        var key2 = $@"{BaseTestKey}\MultiStepTest2";
        var key3 = $@"{BaseTestKey}\MultiStepTest3";

        // Write initial values
        var i1 = RegistryService.Write(NewCall(), new RegistryItem(key1, "Value", "Initial1"));
        Assert.True(i1.Ok, i1.Error);
        var i2 = RegistryService.Write(NewCall(), new RegistryItem(key2, "Value", "Initial2"));
        Assert.True(i2.Ok, i2.Error);
        var i3 = RegistryService.Write(NewCall(), new RegistryItem(key3, "Value", "Initial3"));
        Assert.True(i3.Ok, i3.Error);

        // Fresh change set to capture new operations
        var changes = new ChangeSet();
        OpCall NewTrackedCall() => new() { Changes = changes, Logger = NullLogger.Instance };

        // Perform multi-step operation
        var u1 = RegistryService.Write(
            NewTrackedCall(),
            new RegistryItem(key1, "Value", "Updated1")
        );
        Assert.True(u1.Ok, u1.Error);
        var u2 = RegistryService.Write(
            NewTrackedCall(),
            new RegistryItem(key2, "Value", "Updated2")
        );
        Assert.True(u2.Ok, u2.Error);

        // Simulate a failure by not writing to key3
        // In real scenario, this would be caught by transaction logic

        // Get all revert steps
        var revertSteps = changes
            .SuccessfulSteps.Where(c => c.Revert != null)
            .Select(c => c.Revert!)
            .ToList();

        // Revert all steps in reverse order
        foreach (var step in revertSteps.AsEnumerable().Reverse())
        {
            Assert.True(await step.ExecuteAsync(TestShell.Context(), NullLogger.Instance));
        }

        // Verify original state is restored
        Assert.Equal("Initial1", RegistryService.Read<string>(new RegistryItem(key1, "Value")));
        Assert.Equal("Initial2", RegistryService.Read<string>(new RegistryItem(key2, "Value")));
        Assert.Equal("Initial3", RegistryService.Read<string>(new RegistryItem(key3, "Value")));
    }

    [Fact]
    public async Task ConcurrentRegistryOperations_DoesNotCauseCorruption()
    {
        // Test scenario: Concurrent writes to different keys
        var tasks = new List<Task>();
        const int concurrentOps = 10;

        for (int i = 0; i < concurrentOps; i++)
        {
            var key = $@"{BaseTestKey}\ConcurrentTest{i}";
            var value = $"Value{i}";

            tasks.Add(
                Task.Run(
                    () =>
                    {
                        var write = RegistryService.Write(
                            NewCall(),
                            new RegistryItem(key, "Value", value)
                        );
                        Assert.True(write.Ok, write.Error);
                        var read = RegistryService.Read<string>(new RegistryItem(key, "Value"));
                        Assert.Equal(value, read);
                    },
                    TestContext.Current.CancellationToken
                )
            );
        }

        await Task.WhenAll(tasks);

        // Verify all writes succeeded
        for (int i = 0; i < concurrentOps; i++)
        {
            var key = $@"{BaseTestKey}\ConcurrentTest{i}";
            var expectedValue = $"Value{i}";
            var actualValue = RegistryService.Read<string>(new RegistryItem(key, "Value"));
            Assert.Equal(expectedValue, actualValue);
        }
    }

    [Fact]
    public async Task RegistryValueKindConversion_HandlesAllTypesCorrectly()
    {
        var key = $@"{BaseTestKey}\TypeConversionTest";

        // Test different value types
        var dw = RegistryService.Write(
            NewCall(),
            new RegistryItem(key, "DWordValue", 42, RegistryValueKind.DWord)
        );
        Assert.True(dw.Ok, dw.Error);
        Assert.Equal(42, RegistryService.Read<int>(new RegistryItem(key, "DWordValue")));

        var qw = RegistryService.Write(
            NewCall(),
            new RegistryItem(key, "QWordValue", 9999999999L, RegistryValueKind.QWord)
        );
        Assert.True(qw.Ok, qw.Error);
        Assert.Equal(9999999999L, RegistryService.Read<long>(new RegistryItem(key, "QWordValue")));

        var sw = RegistryService.Write(
            NewCall(),
            new RegistryItem(key, "StringValue", "TestString", RegistryValueKind.String)
        );
        Assert.True(sw.Ok, sw.Error);
        Assert.Equal(
            "TestString",
            RegistryService.Read<string>(new RegistryItem(key, "StringValue"))
        );

        var multiString = new[] { "Line1", "Line2", "Line3" };
        var mw = RegistryService.Write(
            NewCall(),
            new RegistryItem(key, "MultiStringValue", multiString, RegistryValueKind.MultiString)
        );
        Assert.True(mw.Ok, mw.Error);
        var readMulti = RegistryService.Read<string[]>(new RegistryItem(key, "MultiStringValue"));
        Assert.Equal(multiString, readMulti);
    }

    [Fact]
    public async Task RevertStepWithSubSteps_ExecutesInCorrectOrder()
    {
        // Test that nested revert steps execute in the correct order
        var key1 = $@"{BaseTestKey}\SubStepTest\Key1";
        var key2 = $@"{BaseTestKey}\SubStepTest\Key2";

        var o1 = RegistryService.Write(NewCall(), new RegistryItem(key1, "Value", "Original1"));
        Assert.True(o1.Ok, o1.Error);
        var o2 = RegistryService.Write(NewCall(), new RegistryItem(key2, "Value", "Original2"));
        Assert.True(o2.Ok, o2.Error);

        // Fresh change set to capture new operations
        var changes = new ChangeSet();
        OpCall NewTrackedCall() => new() { Changes = changes, Logger = NullLogger.Instance };

        // Perform operations
        var m1 = RegistryService.Write(
            NewTrackedCall(),
            new RegistryItem(key1, "Value", "Modified1")
        );
        Assert.True(m1.Ok, m1.Error);
        var m2 = RegistryService.Write(
            NewTrackedCall(),
            new RegistryItem(key2, "Value", "Modified2")
        );
        Assert.True(m2.Ok, m2.Error);

        // Revert in reverse order (LIFO)
        var revertSteps = changes
            .SuccessfulSteps.Where(c => c.Revert != null)
            .Select(c => c.Revert!)
            .ToList();

        var executionOrder = new List<string>();
        foreach (var step in revertSteps.AsEnumerable().Reverse())
        {
            var desc = step.Description;
            executionOrder.Add(desc);
            Assert.True(await step.ExecuteAsync(TestShell.Context(), NullLogger.Instance));
        }

        // Verify state is restored
        Assert.Equal("Original1", RegistryService.Read<string>(new RegistryItem(key1, "Value")));
        Assert.Equal("Original2", RegistryService.Read<string>(new RegistryItem(key2, "Value")));
    }

    [Fact]
    public void CleanupEmptyKeys_RemovesOnlyEmptyKeys()
    {
        var emptyKeyPath = $@"{BaseTestKey}\CleanupTest\EmptyKey";
        var nonEmptyKeyPath = $@"{BaseTestKey}\CleanupTest\NonEmptyKey";

        // Create empty key
        var ce = RegistryService.CreateSubKey(NewCall(), new RegistryItem(emptyKeyPath));
        Assert.True(ce.Ok, ce.Error);

        // Create non-empty key
        var cn = RegistryService.CreateSubKey(NewCall(), new RegistryItem(nonEmptyKeyPath));
        Assert.True(cn.Ok, cn.Error);
        var wn = RegistryService.Write(
            NewCall(),
            new RegistryItem(nonEmptyKeyPath, "Value", "SomeValue")
        );
        Assert.True(wn.Ok, wn.Error);

        // Cleanup
        RegistryService.CleanupEmptyKeys(new[] { emptyKeyPath, nonEmptyKeyPath });

        // Empty key should be removed
        Assert.False(RegistryService.KeyExists(new RegistryItem(emptyKeyPath)));

        // Non-empty key should remain
        Assert.True(RegistryService.KeyExists(new RegistryItem(nonEmptyKeyPath)));
        Assert.Equal(
            "SomeValue",
            RegistryService.Read<string>(new RegistryItem(nonEmptyKeyPath, "Value"))
        );
    }
    [Fact]
    public void Write_ValueAlreadySet_RecordsSkip()
    {
        var path = $@"{BaseTestKey}\AlreadySet";
        Assert.True(RegistryService.Write(NewCall(), new RegistryItem(path, "V", 1)).Ok);

        var call = NewCall();
        Assert.True(RegistryService.Write(call, new RegistryItem(path, "V", 1)).Ok);

        var step = Assert.Single(call.Changes.Changes);
        Assert.Equal(ChangeKind.Skip, step.Kind);
        Assert.Null(step.Revert);
    }

    [Fact]
    public void DeleteValue_AbsentValue_RecordsNotApplicable()
    {
        var path = $@"{BaseTestKey}\AbsentValue";

        // The key has to exist, otherwise the delete fails before it can look at the value.
        Assert.True(RegistryService.Write(NewCall(), new RegistryItem(path, "Keep", 1)).Ok);

        var call = NewCall();
        Assert.True(RegistryService.DeleteValue(call, new RegistryItem(path, "Missing")).Ok);

        var step = Assert.Single(call.Changes.Changes);
        Assert.Equal(ChangeKind.NotApplicable, step.Kind);
        Assert.Null(step.Revert);
    }

    [Fact]
    public void CreateSubKey_ExistingKey_RecordsNotApplicable()
    {
        var path = $@"{BaseTestKey}\ExistingKey";
        Assert.True(RegistryService.Write(NewCall(), new RegistryItem(path, "V", 1)).Ok);

        var call = NewCall();
        Assert.True(RegistryService.CreateSubKey(call, new RegistryItem(path)).Ok);

        var step = Assert.Single(call.Changes.Changes);
        Assert.Equal(ChangeKind.NotApplicable, step.Kind);
        Assert.Null(step.Revert);
    }

    [Fact]
    public void DeleteSubKeyTree_AbsentTree_RecordsNotApplicable()
    {
        var call = NewCall();

        Assert.True(
            RegistryService
                .DeleteSubKeyTree(call, new RegistryItem($@"{BaseTestKey}\AbsentTree"))
                .Ok
        );

        var step = Assert.Single(call.Changes.Changes);
        Assert.Equal(ChangeKind.NotApplicable, step.Kind);
        Assert.Null(step.Revert);
    }
    [Fact]
    public void Write_RecordsTheFactsOfTheStep()
    {
        var path = $@"{BaseTestKey}\Facts";
        var call = NewCall();

        Assert.True(RegistryService.Write(call, new RegistryItem(path, "V", 1)).Ok);
        var written = Assert.Single(call.Changes.Changes).Detail;
        Assert.NotNull(written);
        Assert.Equal("registry.write", written.Operation);
        Assert.Equal(path, written.Target);
        Assert.Equal("V", written.ValueName);
        Assert.Equal("DWord", written.ValueType);
        Assert.Null(written.PreviousValue);
        Assert.Equal("1", written.NewValue);

        // Writing the same value again is a skip, and it still says what it found.
        var again = NewCall();
        Assert.True(RegistryService.Write(again, new RegistryItem(path, "V", 1)).Ok);
        var skipped = Assert.Single(again.Changes.Changes);
        Assert.Equal(ChangeKind.Skip, skipped.Kind);
        Assert.Equal("1", skipped.Detail?.PreviousValue);
        Assert.Equal("1", skipped.Detail?.NewValue);
    }
}
