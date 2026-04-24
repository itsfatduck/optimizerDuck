using Wpf.Ui.Appearance;

namespace optimizerDuck.Domain.Configuration;

/// <summary>
///     Application settings that can be persisted to disk.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    ///     General application settings.
    /// </summary>
    public AppOptions App { get; set; } = new();

    /// <summary>
    ///     Optimization-related settings.
    /// </summary>
    public OptimizeOptions Optimize { get; set; } = new();

    /// <summary>
    ///     Bloatware removal settings.
    /// </summary>
    public BloatwareOptions Bloatware { get; set; } = new();

    /// <summary>
    ///     General application options.
    /// </summary>
    public sealed class AppOptions
    {
        /// <summary>
        ///     The UI language code (e.g., "en-US").
        /// </summary>
        public string Language { get; set; } = "en-US";

        /// <summary>
        ///     The UI theme (e.g., "Dark").
        /// </summary>
        public ApplicationTheme Theme { get; set; } = ApplicationTheme.Dark;

        /// <summary>
        ///     Whether the user has accepted the legal terms.
        /// </summary>
        public bool LegalAccepted { get; set; } = false;
    }

    /// <summary>
    ///     Optimization-related options.
    /// </summary>
    public sealed class OptimizeOptions
    {
        /// <summary>
        ///     Timeout in milliseconds for shell command execution.
        /// </summary>
        public int ShellTimeoutMs { get; set; } = 120000;

        /// <summary>
        ///     Whether to show the success snackbar after applying an optimization.
        /// </summary>
        public bool ShowCompletionNotification { get; set; } = false;
    }

    /// <summary>
    ///     Bloatware removal options.
    /// </summary>
    public sealed class BloatwareOptions
    {
        /// <summary>
        ///     Whether to remove provisioned AppX packages.
        /// </summary>
        public bool RemoveProvisioned { get; set; } = true;
    }
}
