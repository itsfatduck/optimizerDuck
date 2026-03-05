using System.Collections.ObjectModel;
using Microsoft.Win32.TaskScheduler;
using optimizerDuck.Core.Models.Execution;
using optimizerDuck.Core.Models.Revert.Steps;
using optimizerDuck.Core.Models.ScheduledTask;
using optimizerDuck.Resources.Languages;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace optimizerDuck.Services.OptimizationServices;

public static class ScheduledTaskService
{
    /// <summary>
    ///     Checks whether a task at the given full path exists and is enabled.
    /// </summary>
    public static bool IsTaskEnabled(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath);
            return task is { Enabled: true };
        }
        catch (Exception ex)
        {
            ExecutionScope.LogDebug("Failed to check task enabled state {Path}: {Error}", fullPath, ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Disables a task by full path with logging, tracking, and revert recording.
    /// </summary>
    public static bool DisableTask(string fullPath)
    {
        var description = $"Disable: {fullPath}";
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath) ??
                       throw new InvalidOperationException(string.Format(Translations.ScheduledTasks_Error_TaskNotFound,
                           fullPath));

            var wasEnabled = task.Enabled;
            task.Enabled = false;

            // Record revert step: restore to previous enabled state
            ScheduledTaskRevertStep? revertStep = null;
            if (wasEnabled)
                revertStep = new ScheduledTaskRevertStep
                {
                    FullPath = fullPath,
                    OriginalEnabled = true
                };

            ExecutionScope.LogInfo("Disabled task {Path}", fullPath);
            ExecutionScope.Track(nameof(DisableTask), true);
            ExecutionScope.RecordStep(
                Translations.Service_ScheduledTask_Name,
                description,
                true,
                revertStep);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            ExecutionScope.LogError(null, "Access denied disabling task {Path}", fullPath);
            ExecutionScope.Track(nameof(DisableTask), false);
            ExecutionScope.RecordStep(
                Translations.Service_ScheduledTask_Name,
                description,
                false,
                null,
                Translations.Service_Common_Error_AccessDenied,
                () => System.Threading.Tasks.Task.FromResult(DisableTask(fullPath)));
            return false;
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "Failed to disable task {Path}", fullPath);
            ExecutionScope.Track(nameof(DisableTask), false);
            ExecutionScope.RecordStep(
                Translations.Service_ScheduledTask_Name,
                description,
                false,
                null,
                ex.Message,
                () => System.Threading.Tasks.Task.FromResult(DisableTask(fullPath)));
            return false;
        }
    }

    /// <summary>
    ///     Enables a task by full path with logging, tracking, and revert recording.
    /// </summary>
    public static bool EnableTask(string fullPath)
    {
        var description = $"Enable: {fullPath}";
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath) ??
                       throw new InvalidOperationException(string.Format(Translations.ScheduledTasks_Error_TaskNotFound,
                           fullPath));

            var wasEnabled = task.Enabled;
            task.Enabled = true;

            // Record revert step: restore to previous enabled state
            ScheduledTaskRevertStep? revertStep = null;
            if (!wasEnabled)
                revertStep = new ScheduledTaskRevertStep
                {
                    FullPath = fullPath,
                    OriginalEnabled = false
                };

            ExecutionScope.LogInfo("Enabled task {Path}", fullPath);
            ExecutionScope.Track(nameof(EnableTask), true);
            ExecutionScope.RecordStep(
                Translations.Service_ScheduledTask_Name,
                description,
                true,
                revertStep);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            ExecutionScope.LogError(null, "Access denied enabling task {Path}", fullPath);
            ExecutionScope.Track(nameof(EnableTask), false);
            ExecutionScope.RecordStep(
                Translations.Service_ScheduledTask_Name,
                description,
                false,
                null,
                Translations.Service_Common_Error_AccessDenied,
                () => System.Threading.Tasks.Task.FromResult(EnableTask(fullPath)));
            return false;
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "Failed to enable task {Path}", fullPath);
            ExecutionScope.Track(nameof(EnableTask), false);
            ExecutionScope.RecordStep(
                Translations.Service_ScheduledTask_Name,
                description,
                false,
                null,
                ex.Message,
                () => System.Threading.Tasks.Task.FromResult(EnableTask(fullPath)));
            return false;
        }
    }

    /// <summary>
    ///     Recursively retrieves all scheduled tasks from the system.
    /// </summary>
    public static List<ScheduledTaskModel> GetAllTasks()
    {
        var results = new List<ScheduledTaskModel>();
        try
        {
            using var ts = new TaskService();
            CollectTasks(ts.RootFolder, results);

            // Extract icons from task commands
            Parallel.ForEach(results, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                task =>
                {
                    if (!string.IsNullOrWhiteSpace(task.ActionSummary))
                        task.LogoImage = StartupManagerService.ExtractIcon(task.ActionSummary);
                });
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "Failed to enumerate scheduled tasks");
        }

        return results;
    }

    /// <summary>
    ///     Returns only tasks that have a LogonTrigger or BootTrigger (startup-related).
    /// </summary>
    public static List<ScheduledTaskModel> GetStartupTasks()
    {
        return GetAllTasks()
            .Where(t => t.HasLogonTrigger || t.HasBootTrigger)
            .OrderBy(t => t.Name)
            .ToList();
    }

    public static void RunTask(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath) ?? throw new InvalidOperationException(
                string.Format(Translations.ScheduledTasks_Error_TaskNotFound, fullPath));
            task.Run();
            ExecutionScope.LogInfo("Started task {Path}", fullPath);
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "Failed to run task {Path}", fullPath);
            throw;
        }
    }

    public static void StopTask(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath) ?? throw new InvalidOperationException(
                string.Format(Translations.ScheduledTasks_Error_TaskNotFound, fullPath));
            task.Stop();
            ExecutionScope.LogInfo("Stopped task {Path}", fullPath);
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "Failed to stop task {Path}", fullPath);
            throw;
        }
    }

    /// <summary>
    ///     Gets the current state string of a task by its full path.
    /// </summary>
    public static string? GetTaskState(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath);
            return task?.State.ToString();
        }
        catch (Exception ex)
        {
            ExecutionScope.LogDebug("Failed to get state for task {Path}: {Error}", fullPath, ex.Message);
            return null;
        }
    }

    public static void DeleteTask(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath) ?? throw new InvalidOperationException(
                string.Format(Translations.ScheduledTasks_Error_TaskNotFound, fullPath));
            var folderPath = task.Folder.Path;
            ts.GetFolder(folderPath).DeleteTask(task.Name);
            ExecutionScope.LogInfo("Deleted task {Path}", fullPath);
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "Failed to delete task {Path}", fullPath);
            throw;
        }
    }

    /// <summary>
    ///     [WIP] Registers a new scheduled task from a model definition.
    /// </summary>
    public static void RegisterTask(string folderPath, ScheduledTaskModel model)
    {
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
                td.Triggers.Add(new DailyTrigger { StartBoundary = DateTime.Today + model.DailyTriggerTime });

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
            ExecutionScope.LogInfo("Registered task {Name} in folder {Folder}", model.Name, folderPath);
        }
        catch (Exception ex)
        {
            ExecutionScope.LogError(ex, "Failed to register task {Name} in {Folder}", model.Name, folderPath);
            throw;
        }
    }

    #region Helpers

    private static void CollectTasks(TaskFolder folder, List<ScheduledTaskModel> results)
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
                    ExecutionScope.LogDebug("Failed to map task {Name}: {Error}", task.Name, ex.Message);
                }

            foreach (var subFolder in folder.SubFolders)
                CollectTasks(subFolder, results);
        }
        catch (Exception ex)
        {
            ExecutionScope.LogDebug("Failed to enumerate folder {Path}: {Error}", folder.Path, ex.Message);
        }
    }

    private static ScheduledTaskModel MapTaskToModel(Task task)
    {
        var def = task.Definition;
        var triggers = def.Triggers;
        var actions = def.Actions;

        var triggerDescriptions = triggers
            .Select(t => t.ToString() ?? t.TriggerType.ToString())
            .ToList();

        var actionSummary = string.Empty;
        if (actions.Count > 0 && actions[0] is ExecAction exec)
            actionSummary = string.IsNullOrWhiteSpace(exec.Arguments)
                ? exec.Path ?? string.Empty
                : $"{exec.Path} {exec.Arguments}";

        var hasLogon = triggers.Any(t => t.TriggerType == TaskTriggerType.Logon);
        var hasBoot = triggers.Any(t => t.TriggerType == TaskTriggerType.Boot);

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
            HasBootTrigger = hasBoot
        };
    }

    #endregion Helpers
}