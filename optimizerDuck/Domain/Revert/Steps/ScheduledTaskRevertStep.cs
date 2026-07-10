using Newtonsoft.Json.Linq;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Exceptions;
using optimizerDuck.Resources.Languages;
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
        string.Format(
            OriginalEnabled
                ? Translations.Revert_ScheduledTask_Description_Enable
                : Translations.Revert_ScheduledTask_Description_Disable,
            FullPath
        );

    /// <inheritdoc />
    public Task<bool> ExecuteAsync()
    {
        var success = OriginalEnabled
            ? ScheduledTaskService.EnableTask(FullPath)
            : ScheduledTaskService.DisableTask(FullPath);

        if (!success)
        {
            var error = ScheduledTaskService.LastError ?? Description;
            throw new StepExecutionException(error, ScheduledTaskService.LastErrorDetail);
        }

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
        return new ScheduledTaskRevertStep
        {
            FullPath = data[nameof(FullPath)]?.ToString() ?? string.Empty,
            OriginalEnabled = data[nameof(OriginalEnabled)]?.Value<bool>() ?? true,
        };
    }
}
