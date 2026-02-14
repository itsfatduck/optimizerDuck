using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Models.Revert;
using optimizerDuck.Core.Models.Revert.Steps;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Test.Services.Managers;

public class RevertManagerTests
{
    [Fact]
    public async Task IsAppliedAsync_And_GetRevertDataAsync_HandleMissingFile()
    {
        var id = Guid.NewGuid();

        var isApplied = await RevertManager.IsAppliedAsync(id);
        var data = await RevertManager.GetRevertDataAsync(id);

        Assert.False(isApplied);
        Assert.Null(data);
    }

    [Fact]
    public async Task GetRevertDataAsync_ReadsValidPayload()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        Directory.CreateDirectory(Shared.RevertDirectory);

        var payload = new RevertData
        {
            OptimizationId = id,
            OptimizationName = "TestOptimization",
            AppliedAt = DateTime.UtcNow,
            Steps = new List<RevertStepData>()
        };

        try
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            await File.WriteAllTextAsync(path, json);

            var data = await RevertManager.GetRevertDataAsync(id);

            Assert.NotNull(data);
            Assert.Equal(id, data!.OptimizationId);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RevertAsync_WithInvalidJson_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        Directory.CreateDirectory(Shared.RevertDirectory);

        try
        {
            await File.WriteAllTextAsync(path, "{ invalid json }");

            var manager = new RevertManager(NullLogger<RevertManager>.Instance);
            var result = await manager.RevertAsync(id, "TestOptimization");

            Assert.False(result.Success);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RevertAsync_WithPartialStepFailures_ReturnsFailure_And_KeepsFile()
    {
        var id = Guid.NewGuid();
        var path = Path.Combine(Shared.RevertDirectory, id + ".json");
        Directory.CreateDirectory(Shared.RevertDirectory);

        var payload = new RevertData
        {
            OptimizationId = id,
            OptimizationName = "TestOptimization",
            AppliedAt = DateTime.UtcNow,
            Steps = new List<RevertStepData>
            {
                new()
                {
                    Type = "Shell",
                    Data = new ShellRevertStep
                    {
                        ShellType = ShellType.CMD,
                        Command = "exit 0"
                    }.ToData()
                },
                new()
                {
                    Type = "Shell",
                    Data = new ShellRevertStep
                    {
                        ShellType = ShellType.CMD,
                        Command = "exit 1"
                    }.ToData()
                }
            }
        };

        try
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            await File.WriteAllTextAsync(path, json);

            var manager = new RevertManager(NullLogger<RevertManager>.Instance);
            var result = await manager.RevertAsync(id, "TestOptimization");

            Assert.False(result.Success);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}