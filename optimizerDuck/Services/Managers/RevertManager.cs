using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Execution;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Revert;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Resources.Languages;

namespace optimizerDuck.Services.Managers;

public class RevertManager(ILogger<RevertManager> logger, ILoggerFactory loggerFactory)
{
    private static readonly Dictionary<string, Func<JObject, IRevertStep>> _stepRegistry = BuildStepRegistry();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _fileLocks = new();

    public async Task SaveRevertDataAsync(ExecutionScope scope)
    {
        var steps = scope.SuccessfulSteps
            .Where(s => s.RevertStep != null)
            .Select(s => new RevertStepData { Type = s.RevertStep!.Type, Data = s.RevertStep.ToData() })
            .ToList();

        if (steps.Count == 0) return;

        await WriteJsonAsync(GetFilePath(scope.OptimizationId!.Value), new RevertData
        {
            OptimizationId = scope.OptimizationId!.Value,
            OptimizationName = scope.OptimizationKey!,
            AppliedAt = DateTime.Now,
            Steps = steps
        });
    }

    public async Task<RevertResult> RevertAsync(IOptimization opt, IProgress<ProcessingProgress>? progress = null)
    {
        var l = loggerFactory.CreateLogger<RevertManager>();
        using var scope = ExecutionScope.BeginForLogging(opt.Id, opt.OptimizationKey, l);

        var steps = await LoadStepsAsync(opt.Id);
        if (steps.Count == 0)
            return new RevertResult
                { Success = false, Message = string.Format(Translations.Revert_Error_NoDataFound, opt.Name) };

        var failed = new List<OperationStepResult>();
        var total = steps.Count;

        for (var i = total - 1; i >= 0; i--)
        {
            var (idx, step) = steps[i];
            progress?.Report(new ProcessingProgress
            {
                Message = string.Format(Translations.Optimization_Revert_ExecutingStep, idx, total, step.Type),
                Value = idx, Total = total
            });

            try
            {
                if (!await step.ExecuteAsync()) throw new Exception(Translations.Revert_Error_StepFailed);
            }
            catch (Exception ex)
            {
                failed.Add(new OperationStepResult
                {
                    Index = idx, Name = step.Type, Description = step.Description, Success = false, Error = ex.Message,
                    RetryAction = () => step.ExecuteAsync()
                });
            }
        }

        if (failed.Count < total) Remove(opt.Id);

        return new RevertResult
        {
            Success = failed.Count == 0,
            IsCompleteFailure = failed.Count == total,
            Message = failed.Count == total ? string.Format(Translations.Optimization_Revert_Error_Failed, opt.Name) :
                failed.Count > 0 ? string.Format(Translations.Optimization_Revert_Error_FailedWithSteps, opt.Name,
                    failed.Count) :
                string.Format(Translations.Optimization_Revert_Success, opt.Name),
            FailedStepDetails = failed
        };
    }

    public async Task AppendOrUpdateRevertStepAsync(Guid id, string name, int idx, IRevertStep step)
    {
        var filePath = GetFilePath(id);
        var lockObj = _fileLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

        await lockObj.WaitAsync();
        try
        {
            var data = await LoadAsync(filePath) ?? new RevertData
                { OptimizationId = id, OptimizationName = name, Steps = [] };

            while (data.Steps.Count < idx)
                data.Steps.Add(new RevertStepData { Type = "Unknown", Data = new JObject() });

            data.Steps[idx - 1] = new RevertStepData { Type = step.Type, Data = step.ToData() };
            await WriteJsonAsync(filePath, data);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public static Task<bool> IsAppliedAsync(Guid id)
    {
        return Task.FromResult(File.Exists(GetFilePath(id)));
    }

    public static async Task<RevertData?> GetRevertDataAsync(Guid id)
    {
        return await LoadAsync(GetFilePath(id));
    }

    public static void ClearAllRevertData(ILogger l)
    {
        if (!Directory.Exists(Shared.RevertDirectory)) return;
        foreach (var f in Directory.GetFiles(Shared.RevertDirectory))
            try
            {
                File.Delete(f);
            }
            catch (Exception ex)
            {
                l.LogError(ex, "Failed to delete {File}", f);
            }
    }

    #region Helpers

    private static string GetFilePath(Guid id)
    {
        return Path.Combine(Shared.RevertDirectory, $"{id}.json");
    }

    private static async Task<RevertData?> LoadAsync(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonConvert.DeserializeObject<RevertData>(await File.ReadAllTextAsync(path));
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteJsonAsync(string path, RevertData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(data, Formatting.Indented));
    }

    private async Task<List<(int Index, IRevertStep Step)>> LoadStepsAsync(Guid id)
    {
        var data = await LoadAsync(GetFilePath(id));
        if (data == null || data.Steps.Count == 0) return [];

        var result = new List<(int, IRevertStep)>();
        for (var i = 0; i < data.Steps.Count; i++)
        {
            var step = DeserializeStep(data.Steps[i].Type, data.Steps[i].Data);
            if (step != null) result.Add((i + 1, step));
        }

        return result;
    }

    private void Remove(Guid id, string? name = null)
    {
        try
        {
            var path = GetFilePath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
                logger.LogInformation("Removed revert data for {Name}", name ?? id.ToString());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove revert data for {Id}", id);
        }
    }

    private static Dictionary<string, Func<JObject, IRevertStep>> BuildStepRegistry()
    {
        var dict = new Dictionary<string, Func<JObject, IRevertStep>>();
        foreach (var type in ReflectionHelper.FindImplementationsInLoadedAssemblies<IRevertStep>())
            try
            {
                var instance = (IRevertStep)Activator.CreateInstance(type)!;
                var method = type.GetMethod("FromData", BindingFlags.Static | BindingFlags.Public);
                if (method != null) dict[instance.Type] = data => (IRevertStep)method.Invoke(null, [data])!;
            }
            catch
            {
            }

        return dict;
    }

    private IRevertStep? DeserializeStep(string type, JToken data)
    {
        if (data is not JObject obj) return null;
        return _stepRegistry.TryGetValue(type, out var factory) ? factory(obj) : null;
    }

    #endregion
}