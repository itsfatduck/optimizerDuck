using System.Globalization;

namespace optimizerDuck.Services.System.Primitives;

/// <summary>
/// English-only provider strings: step names, step descriptions and log/error text.
/// These are intentionally NOT in Translations.resx. Revert data, logs and recorded
/// step names must stay in stable English regardless of UI language, so there is
/// nothing for translators to do here. Add new entries to this class, never to
/// the resx. Templates use {0}-style placeholders via <see cref="Format"/>.
/// </summary>
public static class ServiceStrings
{
    public const string CommonErrorAccessDenied = "Access denied";

    public const string RegistryName = "Registry";
    public const string RegistryDescriptionWrite = "Write registry value: {0}\\{1}";
    public const string RegistryDescriptionDelete = "Delete registry value: {0}\\{1}";
    public const string RegistryDescriptionDeleteKey = "Delete registry key {0}";
    public const string RegistryDescriptionCreateKey = "Create registry key {0}";
    public const string RegistryErrorUnauthorizedAccess = "Unauthorized access";
    public const string RegistryErrorCreateOrOpenSubkeyFailed = "Failed to create/open subkey";
    public const string RegistryErrorBackupTruncated =
        "Registry subtree is too large to back up safely; delete aborted for {0}";
    public const string RegistryErrorAccessDeniedProtectedHive = "Access denied (protected hive)";
    public const string RegistryErrorDetailAccessDeniedWrite = "Access denied writing {0}:{1}";
    public const string RegistryErrorDetailAccessDeniedDelete = "Access denied deleting {0}:{1}";
    public const string RegistryErrorDetailAccessDeniedCreateKey = "Access denied creating {0}";
    public const string RegistryErrorDetailAccessDeniedDeleteKeyTree =
        "Access denied deleting key tree {0}";

    public const string ScheduledTaskName = "Scheduled Task";
    public const string ScheduledTaskDescriptionEnable = "Enable scheduled task: {0}";
    public const string ScheduledTaskDescriptionDisable = "Disable scheduled task: {0}";
    public const string ScheduledTaskInfoSkippedNotFound =
        "Scheduled task '{0}' not found (skipped)";
    public const string ScheduledTaskInfoAlreadyConfigured =
        "Scheduled task '{0}' is already {1} (skipped)";
    public const string ScheduledTaskErrorDetailAccessDeniedEnable =
        "Access denied enabling task {0}";
    public const string ScheduledTaskErrorDetailAccessDeniedDisable =
        "Access denied disabling task {0}";

    public const string ServiceName = "Service";
    public const string ServiceDescriptionChange = "Change service '{0}' to {1} startup";
    public const string ServiceErrorChangeStartupTypeFailed =
        "Failed to change startup type for service";
    public const string ServiceErrorChangeStartupTypeFailedTemplate =
        "Failed to change service '{0}' to {1} startup. Windows error {2}: {3}";
    public const string ServiceErrorQueryFailed = "Failed to query service '{0}'";

    public const string RevertDataUnloadableDescription =
        "Unloadable revert data (unknown type '{0}')";
    public const string RevertDataUnknownType =
        "Cannot revert step of unknown type '{0}'. The revert data was written by a different app version.";
    public const string ServiceErrorExceptionOccurred = "Failed to change service '{0}': {1}";
    public const string ServiceInfoNotFound = "Service '{0}' not found (not present)";
    public const string ServiceInfoAlreadyConfigured =
        "Service '{0}' is already set to {1} (skipped)";
    public const string ServiceInfoAccessDenied =
        "Access to service '{0}' is denied by Windows (left unchanged)";

    public const string ShellName = "Shell";

    public const string PowerPlanName = "PowerPlan";

    public const string StartupAppErrorUnsupportedLocation =
        "Cannot toggle startup app '{0}': its location is not supported";

    public const string HibernationName = "Hibernation";
    public const string HibernationDescriptionDisable = "Disable hibernation and Fast Startup";
    public const string HibernationErrorChangeFailed =
        "Failed to change the hibernation file state: {0}";

    public const string UsbPowerName = "USB power";
    public const string UsbPowerDescriptionDisable = "Disable USB power saving for {0} device(s)";
    public const string UsbPowerErrorChangeFailed = "Failed to change USB power saving";
    public const string UsbPowerInfoAlreadyConfigured =
        "USB power saving already disabled for every device (skipped)";
    public const string UsbPowerInfoNoDevices =
        "No USB root hub devices on this machine (nothing to change)";

    /// <summary>Formats a template with invariant culture (same as Loc.Invariant did).</summary>
    public static string Format(string template, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, template, args);
}
