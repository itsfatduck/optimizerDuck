using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.TaskScheduler;
using optimizerDuck.Core.Models.ScheduledTask;
using optimizerDuck.Resources.Languages;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace optimizerDuck.Services.OptimizationServices;

public class ScheduledTaskService(ILogger<ScheduledTaskService> logger)
{
    #region Optimization uses

    /// <summary>
    ///     Checks whether a task at the given full path exists and is enabled.
    ///     Static overload for use from optimizers.
    /// </summary>
    public static bool IsTaskEnabled(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath);
            return task is { Enabled: true };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Disables a task by full path. Static overload for use from optimizers.
    /// </summary>
    public static void DisableTask(string fullPath)
    {
        using var ts = new TaskService();
        var task = ts.GetTask(fullPath) ??
                   throw new InvalidOperationException(string.Format(Translations.ScheduledTasks_Error_TaskNotFound,
                       fullPath));
        task.Enabled = false;
    }

    /// <summary>
    ///     Enables a task by full path. Static overload for use from optimizers.
    /// </summary>
    public static void EnableTask(string fullPath)
    {
        using var ts = new TaskService();
        var task = ts.GetTask(fullPath) ??
                   throw new InvalidOperationException(string.Format(Translations.ScheduledTasks_Error_TaskNotFound,
                       fullPath));
        task.Enabled = true;
    }

    #endregion Optimization uses

    #region Main Methods

    /// <summary>
    ///     Recursively retrieves all scheduled tasks from the system.
    /// </summary>
    public List<ScheduledTaskModel> GetAllTasks()
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
            logger.LogError(ex, "Failed to enumerate scheduled tasks");
        }

        return results;
    }

    /// <summary>
    ///     Returns only tasks that have a LogonTrigger or BootTrigger (startup-related).
    /// </summary>
    public List<ScheduledTaskModel> GetStartupTasks()
    {
        return GetAllTasks()
            .Where(t => t.HasLogonTrigger || t.HasBootTrigger)
            .OrderBy(t => t.Name)
            .ToList();
    }

    public void EnableTaskLogged(string fullPath)
    {
        try
        {
            EnableTask(fullPath);
            logger.LogInformation("Enabled task {Path}", fullPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enable task {Path}", fullPath);
            throw;
        }
    }

    public void DisableTaskLogged(string fullPath)
    {
        try
        {
            DisableTask(fullPath);
            logger.LogInformation("Disabled task {Path}", fullPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to disable task {Path}", fullPath);
            throw;
        }
    }

    public void RunTask(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath) ?? throw new InvalidOperationException($"Task not found: {fullPath}");
            task.Run();
            logger.LogInformation("Started task {Path}", fullPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run task {Path}", fullPath);
            throw;
        }
    }

    public void StopTask(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath) ?? throw new InvalidOperationException($"Task not found: {fullPath}");
            task.Stop();
            logger.LogInformation("Stopped task {Path}", fullPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop task {Path}", fullPath);
            throw;
        }
    }

    /// <summary>
    ///     Gets the current state string of a task by its full path.
    /// </summary>
    public string? GetTaskState(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath);
            return task?.State.ToString();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to get state for task {Path}", fullPath);
            return null;
        }
    }

    public void DeleteTask(string fullPath)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(fullPath) ?? throw new InvalidOperationException($"Task not found: {fullPath}");
            var folderPath = task.Folder.Path;
            ts.GetFolder(folderPath).DeleteTask(task.Name);
            logger.LogInformation("Deleted task {Path}", fullPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete task {Path}", fullPath);
            throw;
        }
    }

    /// <summary>
    ///     [WIP] Registers a new scheduled task from a model definition.
    /// </summary>
    public void RegisterTask(string folderPath, ScheduledTaskModel model)
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
            logger.LogInformation("Registered task {Name} in folder {Folder}", model.Name, folderPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register task {Name} in {Folder}", model.Name, folderPath);
            throw;
        }
    }

    #endregion Main Methods

    #region Helpers

    private void CollectTasks(TaskFolder folder, List<ScheduledTaskModel> results)
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
                    logger.LogDebug(ex, "Failed to map task {Name}", task.Name);
                }

            foreach (var subFolder in folder.SubFolders)
                CollectTasks(subFolder, results);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to enumerate folder {Path}", folder.Path);
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