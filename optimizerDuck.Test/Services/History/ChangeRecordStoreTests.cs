using System.IO;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Services.History;

namespace optimizerDuck.Test.Services.History;

public class ChangeRecordStoreTests : IDisposable
{
    private readonly Guid _id = Guid.NewGuid();

    public void Dispose()
    {
        foreach (var path in new[]
        {
            ChangeRecordStore.PathFor(_id),
            ChangeRecordStore.PathFor(_id) + ".tmp",
        })
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Ignore: nothing else is affected by a leftover report.
            }
        }
    }

    private ChangeRecord NewRecord() =>
        new()
        {
            Id = _id,
            OptimizationKey = "ChangeRecordStoreTest",
            LogName = "Change record store test",
            AppliedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local),
            Outcome = "Success",
            Steps =
            [
                new ChangeRecordStep
                {
                    Name = "Registry",
                    Description = "Write registry value",
                    Kind = ChangeKind.Change,
                },
                new ChangeRecordStep
                {
                    Name = "Service",
                    Description = "Service already configured",
                    Kind = ChangeKind.Skip,
                },
                new ChangeRecordStep
                {
                    Name = "Service",
                    Description = "Windows protects this service",
                    Kind = ChangeKind.Refused,
                },
                new ChangeRecordStep
                {
                    Name = "Scheduled Task",
                    Description = "Failed to disable task",
                    Kind = ChangeKind.Change,
                    Ok = false,
                    Error = "access denied",
                },
            ],
        };

    [Fact]
    public void TryWrite_ThenTryRead_KeepsEveryStep()
    {
        ChangeRecordStore.TryWrite(NewRecord());

        var read = ChangeRecordStore.TryRead(_id);

        Assert.NotNull(read);
        Assert.Equal("ChangeRecordStoreTest", read.OptimizationKey);
        Assert.Equal("Success", read.Outcome);
        Assert.Equal(4, read.Steps.Count);
        Assert.Equal(ChangeKind.Skip, read.Steps[1].Kind);
        Assert.Equal(ChangeKind.Refused, read.Steps[2].Kind);
        Assert.False(read.Steps[3].Ok);
        Assert.Equal(1, read.ChangedCount);
        Assert.Equal(1, read.FailedCount);
    }

    [Fact]
    public void PathFor_IsOutsideTheRevertData()
    {
        // A revert deletes the revert file; the record must not live where that happens.
        var path = ChangeRecordStore.PathFor(_id);

        Assert.StartsWith(Path.GetFullPath(Shared.HistoryDirectory), Path.GetFullPath(path));
        Assert.DoesNotContain(
            Path.GetFullPath(Shared.RevertDirectory),
            Path.GetFullPath(path),
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void TryRead_NoRecord_ReturnsNull()
    {
        Assert.Null(ChangeRecordStore.TryRead(Guid.NewGuid()));
    }

    [Fact]
    public void TryRead_UnreadableFile_ReturnsNullInsteadOfThrowing()
    {
        Directory.CreateDirectory(Shared.HistoryDirectory);
        File.WriteAllText(ChangeRecordStore.PathFor(_id), "{ this is not json");

        Assert.Null(ChangeRecordStore.TryRead(_id));
    }

    [Fact]
    public void TryWrite_LockedFile_DoesNotThrowAndKeepsThePreviousRecord()
    {
        ChangeRecordStore.TryWrite(NewRecord());
        var path = ChangeRecordStore.PathFor(_id);
        var replacement = NewRecord() with { Outcome = "NothingToDo" };

        using (var _ = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // A report is best effort: a locked file must not reach the caller.
            ChangeRecordStore.TryWrite(replacement);
        }

        Assert.Equal("Success", ChangeRecordStore.TryRead(_id)?.Outcome);
    }
}
