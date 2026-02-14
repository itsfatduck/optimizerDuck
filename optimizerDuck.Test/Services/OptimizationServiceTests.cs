using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Revert;
using optimizerDuck.Core.Models.Revert.Steps;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services;
using optimizerDuck.Services.Managers;
using Wpf.Ui;

namespace optimizerDuck.Test.Services;

public class OptimizationServiceTests
{
    [Fact]
    public async Task ApplyAsync_Success_PersistsRevertDataFile()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = _ =>
                {
                    RevertManager.Record(new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" });
                    ServiceTracker.TrackStep("Test", "Test step", true);
                    return Task.FromResult(ApplyResult.True());
                }
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.ApplyAsync(optimization, progress);

                Assert.Equal(OptimizationSuccessResult.Success, result.Status);
                Assert.True(File.Exists(revertPath));

                var data = await RevertManager.GetRevertDataAsync(optimization.Id);
                Assert.NotNull(data);
                Assert.Equal(optimization.Id, data!.OptimizationId);
                Assert.NotEmpty(data.Steps);
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task ApplyAsync_FailureMessage_TriggersAutoRevert_And_RemovesRevertDataFile()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = _ =>
                {
                    RevertManager.Record(new ShellRevertStep { ShellType = ShellType.CMD, Command = "exit 0" });
                    ServiceTracker.TrackStep("Test", "Failed step", false, "fail", () => Task.FromResult(false));
                    return Task.FromResult(ApplyResult.False("apply failed"));
                }
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.ApplyAsync(optimization, progress);

                Assert.Equal(OptimizationSuccessResult.Failed, result.Status);
                Assert.False(File.Exists(revertPath));
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    [Fact]
    public async Task RevertAsync_WithValidRevertData_RemovesFile()
    {
        await RunInStaThreadAsync(async () =>
        {
            var optimization = new FakeOptimization
            {
                ApplyImpl = _ => Task.FromResult(ApplyResult.True())
            };

            var revertPath = GetRevertFilePath(optimization.Id);

            try
            {
                Directory.CreateDirectory(Shared.RevertDirectory);

                var payload = new RevertData
                {
                    OptimizationId = optimization.Id,
                    OptimizationName = optimization.OptimizationKey,
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
                        }
                    }
                };

                var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
                await File.WriteAllTextAsync(revertPath, json);

                var service = CreateService();
                var progress = new Progress<ProcessingProgress>(_ => { });

                var result = await service.RevertAsync(optimization, progress);

                Assert.True(result.Success);
                Assert.False(File.Exists(revertPath));
            }
            finally
            {
                if (File.Exists(revertPath))
                    File.Delete(revertPath);
            }
        });
    }

    private static OptimizationService CreateService()
    {
        var revertManager = new RevertManager(NullLogger<RevertManager>.Instance);
        var loggerFactory = NullLoggerFactory.Instance;
        var systemInfoService = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var streamService = new StreamService(NullLogger<StreamService>.Instance);
        var contentDialogService = new ContentDialogService();
        var logger = NullLogger<OptimizationService>.Instance;
        return new OptimizationService(revertManager, loggerFactory, systemInfoService, streamService,
            contentDialogService, logger);
    }

    private static string GetRevertFilePath(Guid id)
    {
        return Path.Combine(Shared.RevertDirectory, id + ".json");
    }

    private static Task RunInStaThreadAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource();

        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }

    private sealed class FakeOptimization : IOptimization
    {
        public Type? OwnerType { get; set; }
        public string OwnerKey { get; } = "Test";
        public RiskVisual RiskVisual { get; } = new();
        public IEnumerable<OptimizationTagDisplay> TagDisplays { get; } = [];

        public string Prefix { get; } = "Test";
        public string ProgressPrefix { get; } = "Test";

        public Func<(IProgress<ProcessingProgress> progress, OptimizationContext context), Task<ApplyResult>> ApplyImpl
        {
            get;
            init;
        } =
            _ => Task.FromResult(ApplyResult.True());

        public Guid Id { get; } = Guid.NewGuid();
        public OptimizationRisk Risk { get; } = OptimizationRisk.Safe;
        public string OptimizationKey { get; } = "TestOptimization";

        public string Name { get; } = "Test";
        public string ShortDescription { get; } = "Test";
        public OptimizationState State { get; set; } = null!;

        public Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            return ApplyImpl((progress, context));
        }
    }
}