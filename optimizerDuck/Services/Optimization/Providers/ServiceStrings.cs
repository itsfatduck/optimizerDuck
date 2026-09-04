using System.Globalization;

namespace optimizerDuck.Services.Optimization.Providers;

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
    public const string ScheduledTaskErrorDetailAccessDeniedEnable =
        "Access denied enabling task {0}";
    public const string ScheduledTaskErrorDetailAccessDeniedDisable =
        "Access denied disabling task {0}";

    public const string ServiceName = "Service";
    public const string ServiceDescriptionChange = "Change service '{0}' to {1} startup";
    public const string ServiceErrorChangeStartupTypeFailed =
        "Failed to change startup type for service";
    public const string ServiceErrorExceptionOccurred = "Failed to change service '{0}': {1}";
    public const string ServiceInfoSkippedNotFound = "Service '{0}' not found (skipped)";
    public const string ServiceInfoAlreadyConfigured =
        "Service '{0}' is already set to {1} (skipped)";
    public const string ServiceInfoSkippedAccessDenied =
        "Access to service '{0}' is denied by Windows (skipped)";

    public const string ShellName = "Shell";

    /// <summary>Formats a template with invariant culture (same as Loc.Invariant did).</summary>
    public static string Format(string template, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, template, args);
}
