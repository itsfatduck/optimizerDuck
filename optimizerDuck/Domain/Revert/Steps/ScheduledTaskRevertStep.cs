using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization.Providers;

namespace optimizerDuck.Domain.Revert.Steps;

/// <summary>
///     Represents a revert step that restores a scheduled task to its original enabled/disabled state.
/// </summary>
public class ScheduledTaskRevertStep : IRevertStep
{
    /// <summary>
    ///     Gets or sets the full path of the scheduled task (e.g., <c>\Microsoft\Windows\...</c>).
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a value that indicates whether the task was originally enabled before the optimization.
    /// </summary>
    public bool OriginalEnabled { get; set; }

    /// <inheritdoc />
    public string Type => "ScheduledTask";

    /// <inheritdoc />
    public string Description =>
        OriginalEnabled
            ? Loc.Instance["Revert.ScheduledTask.Description.Enable", FullPath]
            : Loc.Instance["Revert.ScheduledTask.Description.Disable", FullPath];

    /// <inheritdoc />
    public Task<bool> ExecuteAsync(ShellService _, ILogger logger)
    {
        var opCall = new OpCall { Logger = logger };
        var result = OriginalEnabled
            ? ScheduledTaskService.EnableTask(opCall, FullPath)
            : ScheduledTaskService.DisableTask(opCall, FullPath);

        if (!result.Ok)
            throw new StepExecutionException(result.Error ?? Description, result.ErrorDetail);

        // Read-back verify: task must report the restored enabled state afterward.
        var actualState = ScheduledTaskService.IsTaskEnabled(FullPath, opCall.Logger);
        if (actualState != OriginalEnabled)
            throw new StepExecutionException(
                $"Scheduled task verify failed at {FullPath}: expected enabled={OriginalEnabled}, actual={actualState}",
                null
            );

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public JObject ToData()
    {
        return new JObject
        {
            [nameof(FullPath)] = FullPath,
            [nameof(OriginalEnabled)] = OriginalEnabled,
        };
    }

    /// <summary>
    ///     Deserializes a <see cref="ScheduledTaskRevertStep" /> from JSON data.
    /// </summary>
    /// <param name="data">The JSON data to deserialize.</param>
    /// <returns>A new <see cref="ScheduledTaskRevertStep" /> instance.</returns>
    public static ScheduledTaskRevertStep FromData(JObject data)
    {
        var enabledToken = data[nameof(OriginalEnabled)];
        if (enabledToken == null || enabledToken.Type == JTokenType.Null)
            throw new StepExecutionException(
                $"Missing required '{nameof(OriginalEnabled)}' in scheduled-task revert data.",
                data.ToString()
            );
        return new ScheduledTaskRevertStep
        {
            FullPath = data[nameof(FullPath)]?.ToString() ?? string.Empty,
            OriginalEnabled = enabledToken.Value<bool>(),
        };
    }
}
