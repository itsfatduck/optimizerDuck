using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Services.System;

/// <summary>
///     Lightweight, read-only probes that determine whether Windows currently matches an
///     optimization's target state.
/// </summary>
public static class StateProbe
{
    /// <summary>
    ///     Returns <see langword="true" /> when every registry item reads back with its expected value,
    ///     <see langword="false" /> when any is missing or differs, or <see langword="null" /> on read error.
    /// </summary>
    public static bool? RegistryMatches(params RegistryItem[] items)
    {
        if (items.Length == 0)
            return null;

        foreach (var item in items)
        {
            if (!RegistryService.TryReadValue(item, out var actual))
                return null;

            if (actual is null || !RegistryValues.Equal(actual, item.Value, item.Kind))
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Returns <see langword="true" /> when the specified registry value is absent,
    ///     <see langword="false" /> if present, or <see langword="null" /> on read error.
    /// </summary>
    public static bool? RegistryValueAbsent(string path, string name)
    {
        if (!RegistryService.TryReadValue(new RegistryItem(path, name), out var actual))
            return null;

        return actual is null;
    }

    /// <summary>
    ///     Returns <see langword="true" /> when every existing service in <paramref name="items" />
    ///     is at least as strict as its target startup type. Returns <see langword="null" /> when no
    ///     listed service exists on this machine or when a query fails.
    /// </summary>
    public static bool? ServicesAtLeast(params ServiceItem[] items)
    {
        if (items.Length == 0)
            return null;

        var existingMatched = 0;

        foreach (var item in items)
        {
            var (startupType, notFound) = ServiceProcessService.GetStartupType(item.Name);

            if (notFound)
                continue;

            if (startupType is null)
                return null;

            if (
                ServiceProcessService.StrictnessRank(startupType.Value)
                < ServiceProcessService.StrictnessRank(item.StartupType)
            )
                return false;

            existingMatched++;
        }

        return existingMatched > 0 ? true : null;
    }

    /// <summary>
    ///     Checks whether existing scheduled tasks are disabled. Missing tasks are ignored when
    ///     <paramref name="requireAnyPresent" /> is <see langword="false" /> (useful on builds that
    ///     omit legacy tasks).
    /// </summary>
    public static bool? TasksDisabled(bool requireAnyPresent = false, params string[] taskPaths)
    {
        if (taskPaths.Length == 0)
            return null;

        var disabledCount = 0;

        foreach (var taskPath in taskPaths)
        {
            var state = ScheduledTaskService.GetTaskEnabledState(taskPath);
            switch (state)
            {
                case TaskEnabledState.Enabled:
                    return false;
                case TaskEnabledState.Unknown:
                    return null;
                case TaskEnabledState.Disabled:
                    disabledCount++;
                    break;
                case TaskEnabledState.NotFound:
                    break;
            }
        }

        if (disabledCount > 0)
            return true;

        return requireAnyPresent ? null : true;
    }

    /// <summary>
    ///     Checks whether the currently active Windows power scheme matches <paramref name="schemeId" />.
    ///     Returns <see langword="null" /> when <paramref name="schemeId" /> is empty or the active
    ///     scheme cannot be queried.
    /// </summary>
    public static bool? ActivePowerSchemeIs(Guid schemeId)
    {
        if (schemeId == Guid.Empty)
            return null;

        var active = PowerPlanService.QueryActiveSchemeId();
        return active is null ? null : active.Value == schemeId;
    }

    /// <summary>
    ///     Combines multiple nullable probe checks: returns <see langword="false" /> if any check is
    ///     <see langword="false" />, <see langword="null" /> if any check is <see langword="null" />,
    ///     otherwise <see langword="true" />.
    /// </summary>
    public static bool? All(params bool?[] checks)
    {
        if (checks.Length == 0)
            return null;

        var hasUnknown = false;
        foreach (var check in checks)
        {
            if (check == false)
                return false;

            if (check is null)
                hasUnknown = true;
        }

        return hasUnknown ? null : true;
    }
}
