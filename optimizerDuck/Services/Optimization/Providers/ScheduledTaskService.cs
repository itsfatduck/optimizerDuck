using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.TaskScheduler;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Revert.Steps;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.UI;
using ScheduledTaskModel = optimizerDuck.Domain.Optimizations.Models.ScheduledTask.ScheduledTaskModel;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace optimizerDuck.Services.Optimization.Providers;

public static class ScheduledTaskService
{
    /// <summary>Checks whether a task at the given full path exists and is enabled.</summary>
    public static bool IsTaskEnabled(string fullPath, ILogger? logger = null)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath);
            return task is { Enabled: true };
        }
        catch (Exception ex)
        {
            logger?.LogDebug(
                "Failed to check task enabled state {Path}: {Error}",
                fullPath,
                ex.Message
            );
            return false;
        }
    }

    /// <summary>Disables a scheduled task, recording the change into <paramref name="call"/>.</summary>
    /// <param name="call">The explicit call context: change collector, logger and cancellation token.</param>
    /// <param name="fullPath">The full path of the task to disable.</param>
    /// <returns>The outcome of the disable request.</returns>
    public static OpResult DisableTask(OpCall call, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(call);

        var description = ServiceStrings.Format(
            ServiceStrings.ScheduledTaskDescriptionDisable,
            fullPath
        );
        try
        {
            using var ts = new TaskService();
            var task =
                ts.GetTask(fullPath)
                ?? throw new InvalidOperationException(
                    Loc.Instance["ScheduledTasks.Error.TaskNotFound", fullPath]
                );

            var wasEnabled = task.Enabled;
            task.Enabled = false;

            // Record revert step: restore to previous enabled state
            ScheduledTaskRevertStep? revertStep = null;
            if (wasEnabled)
                revertStep = new ScheduledTaskRevertStep
                {
                    FullPath = fullPath,
                    OriginalEnabled = true,
                };

            call.Logger.LogInformation("Disabled task {Path}", fullPath);
            call.Changes.Add(ServiceStrings.ScheduledTaskName, description, true, revertStep);
            return OpResult.Success(revertStep);
        }
        catch (UnauthorizedAccessException ex)
        {
            var error = ServiceStrings.CommonErrorAccessDenied;
            var errorDetail = ServiceStrings.Format(
                ServiceStrings.ScheduledTaskErrorDetailAccessDeniedDisable,
                fullPath
            );
            call.Logger.LogError(ex, "Access denied disabling task {Path}", fullPath);
            call.Changes.Add(
                ServiceStrings.ScheduledTaskName,
                description,
                false,
                null,
                error,
                errorDetail,
                retryCall =>
                    global::System.Threading.Tasks.Task.FromResult(DisableTask(retryCall, fullPath))
            );
            return OpResult.Fail(error, errorDetail);
        }
        catch (Exception ex)
        {
            var error = ex.Message;
            var errorDetail = ex.ToString();
            call.Logger.LogError(ex, "Failed to disable task {Path}", fullPath);
            call.Changes.Add(
                ServiceStrings.ScheduledTaskName,
                description,
                false,
                null,
                error,
                errorDetail,
                retryCall =>
                    global::System.Threading.Tasks.Task.FromResult(DisableTask(retryCall, fullPath))
            );
            return OpResult.Fail(error, errorDetail);
        }
    }

    /// <summary>Enables a scheduled task, recording the change into <paramref name="call"/>.</summary>
    /// <param name="call">The explicit call context: change collector, logger and cancellation token.</param>
    /// <param name="fullPath">The full path of the task to enable.</param>
    /// <returns>The outcome of the enable request.</returns>
    public static OpResult EnableTask(OpCall call, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(call);

        var description = ServiceStrings.Format(
            ServiceStrings.ScheduledTaskDescriptionEnable,
            fullPath
        );
        try
        {
            using var ts = new TaskService();
            var task =
                ts.GetTask(fullPath)
                ?? throw new InvalidOperationException(
                    Loc.Instance["ScheduledTasks.Error.TaskNotFound", fullPath]
                );

            var wasEnabled = task.Enabled;
            task.Enabled = true;

            // Record revert step: restore to previous enabled state
            ScheduledTaskRevertStep? revertStep = null;
            if (!wasEnabled)
                revertStep = new ScheduledTaskRevertStep
                {
                    FullPath = fullPath,
                    OriginalEnabled = false,
                };

            call.Logger.LogInformation("Enabled task {Path}", fullPath);
            call.Changes.Add(ServiceStrings.ScheduledTaskName, description, true, revertStep);
            return OpResult.Success(revertStep);
        }
        catch (UnauthorizedAccessException ex)
        {
            var error = ServiceStrings.CommonErrorAccessDenied;
            var errorDetail = ServiceStrings.Format(
                ServiceStrings.ScheduledTaskErrorDetailAccessDeniedEnable,
                fullPath
            );
            call.Logger.LogError(ex, "Access denied enabling task {Path}", fullPath);
            call.Changes.Add(
                ServiceStrings.ScheduledTaskName,
                description,
                false,
                null,
                error,
                errorDetail,
                retryCall =>
                    global::System.Threading.Tasks.Task.FromResult(EnableTask(retryCall, fullPath))
            );
            return OpResult.Fail(error, errorDetail);
        }
        catch (Exception ex)
        {
            var error = ex.Message;
            var errorDetail = ex.ToString();
            call.Logger.LogError(ex, "Failed to enable task {Path}", fullPath);
            call.Changes.Add(
                ServiceStrings.ScheduledTaskName,
                description,
                false,
                null,
                error,
                errorDetail,
                retryCall =>
                    global::System.Threading.Tasks.Task.FromResult(EnableTask(retryCall, fullPath))
            );
            return OpResult.Fail(error, errorDetail);
        }
    }

    /// <summary>Retrieves all scheduled tasks from the system, including icon extraction.</summary>
    /// <returns>A list of all scheduled tasks.</returns>
    public static List<ScheduledTaskModel> GetAllTasks(ILogger? logger = null)
    {
        var results = new List<ScheduledTaskModel>();
        try
        {
            using var ts = new TaskService();
            CollectTasks(ts.RootFolder, results, logger);

            // Extract icons from task commands
            Parallel.ForEach(
                results,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                task =>
                {
                    if (!string.IsNullOrWhiteSpace(task.ActionSummary))
                        task.LogoImage = StartupManagerService.ExtractIcon(task.ActionSummary);
                }
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to enumerate scheduled tasks");
        }

        return results;
    }

    /// <summary>Returns startup-related tasks: those with a LogonTrigger or BootTrigger.</summary>
    /// <returns>A list of startup-related tasks ordered by name.</returns>
    public static List<ScheduledTaskModel> GetStartupTasks(ILogger? logger = null)
    {
        return GetAllTasks(logger)
            .Where(t => t.HasLogonTrigger || t.HasBootTrigger)
            .OrderBy(t => t.Name)
            .ToList();
    }

    /// <summary>Runs a scheduled task immediately.</summary>
    /// <param name="call">Optional call context used for change recording and logging.</param>
    /// <param name="fullPath">The full path of the task to run.</param>
    /// <returns>The outcome of the run request. No revert step: starting a task is not reversible.</returns>
    public static OpResult RunTask(OpCall? call, string fullPath)
    {
        var description = ServiceStrings.Format("Run scheduled task: {0}", fullPath);

        try
        {
            using var ts = new TaskService();
            var task =
                ts.GetTask(fullPath)
                ?? throw new InvalidOperationException(
                    Loc.Instance["ScheduledTasks.Error.TaskNotFound", fullPath]
                );
            task.Run();
            call?.Logger.LogInformation("Started task {Path}", fullPath);
            call?.Changes.Add(ServiceStrings.ScheduledTaskName, description, true);
            return OpResult.Success();
        }
        catch (Exception ex)
        {
            var error = ex.Message;
            var errorDetail = ex.ToString();
            call?.Logger.LogError(ex, "Failed to run task {Path}", fullPath);
            call?.Changes.Add(
                ServiceStrings.ScheduledTaskName,
                description,
                false,
                null,
                error,
                errorDetail,
                retryCall =>
                    global::System.Threading.Tasks.Task.FromResult(RunTask(retryCall, fullPath))
            );
            return OpResult.Fail(error, errorDetail);
        }
    }

    /// <summary>Stops a running scheduled task.</summary>
    /// <param name="call">Optional call context used for change recording and logging.</param>
    /// <param name="fullPath">The full path of the task to stop.</param>
    /// <returns>The outcome of the stop request. No revert step: stopping a task is not reversible.</returns>
    public static OpResult StopTask(OpCall? call, string fullPath)
    {
        var description = ServiceStrings.Format("Stop scheduled task: {0}", fullPath);

        try
        {
            using var ts = new TaskService();
            var task =
                ts.GetTask(fullPath)
                ?? throw new InvalidOperationException(
                    Loc.Instance["ScheduledTasks.Error.TaskNotFound", fullPath]
                );
            task.Stop();
            call?.Logger.LogInformation("Stopped task {Path}", fullPath);
            call?.Changes.Add(ServiceStrings.ScheduledTaskName, description, true);
            return OpResult.Success();
        }
        catch (Exception ex)
        {
            var error = ex.Message;
            var errorDetail = ex.ToString();
            call?.Logger.LogError(ex, "Failed to stop task {Path}", fullPath);
            call?.Changes.Add(
                ServiceStrings.ScheduledTaskName,
                description,
                false,
                null,
                error,
                errorDetail,
                retryCall =>
                    global::System.Threading.Tasks.Task.FromResult(StopTask(retryCall, fullPath))
            );
            return OpResult.Fail(error, errorDetail);
        }
    }

    /// <summary>Gets the current state string of a task.</summary>
    /// <param name="fullPath">The full path of the task.</param>
    /// <param name="logger">Optional logger used only for logging.</param>
    /// <returns>The task state string, or <see langword="null" /> if the task is not found or an error occurs.</returns>
    public static string? GetTaskState(string fullPath, ILogger? logger = null)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath);
            return task?.State.ToString();
        }
        catch (Exception ex)
        {
            logger?.LogDebug("Failed to get state for task {Path}: {Error}", fullPath, ex.Message);
            return null;
        }
    }

    /// <summary>Deletes a scheduled task. This is destructive and cannot be undone.</summary>
    /// <param name="call">Optional call context used for change recording and logging.</param>
    /// <param name="fullPath">The full path of the task to delete.</param>
    /// <returns>The outcome of the delete request. No revert step.</returns>
    public static OpResult DeleteTask(OpCall? call, string fullPath)
    {
        var description = ServiceStrings.Format(
            "Delete scheduled task: {0} (cannot be undone)",
            fullPath
        );

        try
        {
            using var ts = new TaskService();
            var task =
                ts.GetTask(fullPath)
                ?? throw new InvalidOperationException(
                    Loc.Instance["ScheduledTasks.Error.TaskNotFound", fullPath]
                );
            var folderPath = task.Folder.Path;
            ts.GetFolder(folderPath).DeleteTask(task.Name);
            call?.Logger.LogInformation("Deleted task {Path}", fullPath);
            call?.Changes.Add(ServiceStrings.ScheduledTaskName, description, true);
            return OpResult.Success();
        }
        catch (Exception ex)
        {
            var error = ex.Message;
            var errorDetail = ex.ToString();
            call?.Logger.LogError(ex, "Failed to delete task {Path}", fullPath);
            call?.Changes.Add(
                ServiceStrings.ScheduledTaskName,
                description,
                false,
                null,
                error,
                errorDetail,
                retryCall =>
                    global::System.Threading.Tasks.Task.FromResult(DeleteTask(retryCall, fullPath))
            );
            return OpResult.Fail(error, errorDetail);
        }
    }

    /// <summary>Registers a new scheduled task from a model definition. Overwrites any existing task with the same name.</summary>
    /// <param name="call">Optional call context used for change recording and logging.</param>
    /// <param name="folderPath">The target folder path (e.g. <c>\MyApp</c>).</param>
    /// <param name="model">The task definition model.</param>
    /// <returns>The outcome of the registration. No revert step.</returns>
    public static OpResult RegisterTask(OpCall? call, string folderPath, ScheduledTaskModel model)
    {
        var displayPath =
            string.IsNullOrWhiteSpace(folderPath) || folderPath == "\\"
                ? $"\\{model.Name}"
                : $"{folderPath.TrimEnd('\\')}\\{model.Name}";
        var description = ServiceStrings.Format(
            "Register scheduled task: {0} (overwrites existing task, cannot be undone)",
            displayPath
        );

        try
        {
            using var ts = new TaskService();
            var td = ts.NewTask();
            td.RegistrationInfo.Description = model.Description ?? string.Empty;
            td.RegistrationInfo.Author = model.Author ?? string.Empty;
            td.Settings.Enabled = model.IsEnabled;
            td.Settings.Hidden = model.Hidden;

            if (model.RunWithHighestPrivileges)
                td.Principal.RunLevel = TaskRunLevel.Highest;

            // Handle Action Execution accurately
            if (!string.IsNullOrWhiteSpace(model.ExecutablePath))
            {
                var action = new ExecAction(model.ExecutablePath);
                if (!string.IsNullOrWhiteSpace(model.Arguments))
                    action.Arguments = model.Arguments;
                td.Actions.Add(action);
            }
            else if (!string.IsNullOrWhiteSpace(model.ActionSummary)) // Fallback if still populated via old approach
            {
                var parts = model.ActionSummary.Trim();
                var spaceIdx = parts.IndexOf(' ');
                if (spaceIdx > 0)
                    td.Actions.Add(new ExecAction(parts[..spaceIdx], parts[(spaceIdx + 1)..]));
                else
                    td.Actions.Add(new ExecAction(parts));
            }

            // Add triggers based on model flags
            if (model.HasLogonTrigger)
                td.Triggers.Add(new LogonTrigger());
            if (model.HasBootTrigger)
                td.Triggers.Add(new BootTrigger());
            if (model.HasIdleTrigger)
                td.Triggers.Add(new IdleTrigger());
            if (model.HasRegistrationTrigger)
                td.Triggers.Add(new RegistrationTrigger());
            if (model.HasDailyTrigger)
                td.Triggers.Add(
                    new DailyTrigger { StartBoundary = DateTime.Today + model.DailyTriggerTime }
                );

            // Ensure folder exists
            var folder = ts.RootFolder;
            if (!string.IsNullOrWhiteSpace(folderPath) && folderPath != "\\")
                try
                {
                    folder = ts.GetFolder(folderPath);
                }
                catch
                {
                    folder = ts.RootFolder.CreateFolder(folderPath);
                }

            folder.RegisterTaskDefinition(model.Name, td);
            call?.Logger.LogInformation(
                "Registered task {Name} in folder {Folder}",
                model.Name,
                folderPath
            );
            call?.Changes.Add(ServiceStrings.ScheduledTaskName, description, true);
            return OpResult.Success();
        }
        catch (Exception ex)
        {
            var error = ex.Message;
            var errorDetail = ex.ToString();
            call?.Logger.LogError(
                ex,
                "Failed to register task {Name} in {Folder}",
                model.Name,
                folderPath
            );
            call?.Changes.Add(
                ServiceStrings.ScheduledTaskName,
                description,
                false,
                null,
                error,
                errorDetail,
                retryCall =>
                    global::System.Threading.Tasks.Task.FromResult(
                        RegisterTask(retryCall, folderPath, model)
                    )
            );
            return OpResult.Fail(error, errorDetail);
        }
    }

    #region Helpers

    private static void CollectTasks(
        TaskFolder folder,
        List<ScheduledTaskModel> results,
        ILogger? logger
    )
    {
        try
        {
            foreach (var task in folder.Tasks)
                try
                {
                    results.Add(MapTaskToModel(task));
                }
                catch (Exception ex)
                {
                    logger?.LogDebug("Failed to map task {Name}: {Error}", task.Name, ex.Message);
                }

            foreach (var subFolder in folder.SubFolders)
                CollectTasks(subFolder, results, logger);
        }
        catch (Exception ex)
        {
            logger?.LogDebug("Failed to enumerate folder {Path}: {Error}", folder.Path, ex.Message);
        }
    }

    private static ScheduledTaskModel MapTaskToModel(Task task)
    {
        var def = task.Definition;
        var triggers = def.Triggers;
        var actions = def.Actions;

        var triggerDescriptions = new List<string>();
        var hasLogon = false;
        var hasBoot = false;

        foreach (var t in triggers)
        {
            triggerDescriptions.Add(t.ToString() ?? t.TriggerType.ToString());
            if (t.TriggerType == TaskTriggerType.Logon)
                hasLogon = true;
            if (t.TriggerType == TaskTriggerType.Boot)
                hasBoot = true;
        }

        var actionSummary = string.Empty;
        if (actions.Count > 0 && actions[0] is ExecAction exec)
            actionSummary = string.IsNullOrWhiteSpace(exec.Arguments)
                ? exec.Path ?? string.Empty
                : $"{exec.Path} {exec.Arguments}";

        return new ScheduledTaskModel
        {
            Name = task.Name,
            Path = task.Folder.Path,
            FullPath = task.Path,
            Description = def.RegistrationInfo.Description,
            Author = def.RegistrationInfo.Author,
            IsEnabled = task.Enabled,
            State = task.State.ToString(),
            TriggerSummary = string.Join("; ", triggerDescriptions),
            TriggerTypes = new ObservableCollection<string>(triggerDescriptions),
            ActionSummary = actionSummary,
            LastRunTime = task.LastRunTime == DateTime.MinValue ? null : task.LastRunTime,
            NextRunTime = task.NextRunTime == DateTime.MinValue ? null : task.NextRunTime,
            LastRunResult = task.LastTaskResult,
            HasLogonTrigger = hasLogon,
            HasBootTrigger = hasBoot,
        };
    }

    #endregion Helpers
}
