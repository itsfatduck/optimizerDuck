using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Services.System;

public class UpdaterService : IDisposable
{
    private const string Owner = "itsfatduck";
    private const string Repo = "optimizerDuck";

    /// <summary>The URL to the latest release page on GitHub.</summary>
    public const string LatestReleaseUrl = $"https://github.com/{Owner}/{Repo}/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="UpdaterService"/> class.</summary>
    /// <param name="logger">The logger for update diagnostics.</param>
    public UpdaterService(ILogger<UpdaterService> logger)
    {
        _httpClient = HttpClientFactory.CreateClient(logger: logger);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(BuildUserAgentHeader());
        _logger = logger;
    }

    /// <summary>
    ///     Builds the user agent header that identifies this build to the release host.
    /// </summary>
    /// <returns>The user agent header.</returns>
    internal static ProductInfoHeaderValue BuildUserAgentHeader()
    {
        return new ProductInfoHeaderValue("optimizerDuck", Shared.FileVersion);
    }

    /// <summary>Parses the version a release tag names.</summary>
    /// <param name="tagName">The release tag, for example "v1.2.3".</param>
    /// <returns>The version the tag names, or <see langword="null" /> when it names none.</returns>
    internal static Version? ParseReleaseTag(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return null;

        // "v1.1.0" becomes "1.1.0", then "1.1.0-fix" becomes "1.1.0".
        var text = tagName.TrimStart('v');
        var preReleaseSeparatorIndex = text.IndexOf('-');
        if (preReleaseSeparatorIndex != -1)
            text = text[..preReleaseSeparatorIndex];

        return Version.TryParse(text, out var version) ? version : null;
    }

    /// <summary>Finds the installable executable in a release.</summary>
    /// <param name="release">The release as the host reported it.</param>
    /// <returns>
    ///     The executable asset, or <see langword="null" /> when the release carries none.
    /// </returns>
    internal static GitHubAsset? FindExecutableAsset(GitHubRelease? release)
    {
        return release?.Assets.FirstOrDefault(asset =>
            asset.Name.StartsWith("optimizerDuck", StringComparison.OrdinalIgnoreCase)
            && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>Checks the GitHub API for a newer version of the application.</summary>
    /// <returns>
    ///     The newer version string, or <see langword="null" /> when the running version is current or
    ///     the check could not determine a newer one.
    /// </returns>
    public async Task<string?> CheckForUpdatesAsync()
    {
        _logger.LogInformation(
            "Checking for updates (Current version: {CurrentVersion})",
            Shared.FileVersion
        );

        try
        {
            var response = await _httpClient.GetStringAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest"
            );
            var latestRelease = JsonConvert.DeserializeObject<GitHubRelease>(response);

            if (latestRelease == null || string.IsNullOrEmpty(latestRelease.TagName))
            {
                _logger.LogWarning("Could not retrieve latest release information");
                return null;
            }

            _logger.LogDebug(
                "Latest release version: {LatestReleaseTagName}",
                latestRelease.TagName
            );

            var latestVersion = ParseReleaseTag(latestRelease.TagName);
            if (latestVersion is null)
            {
                _logger.LogWarning(
                    "Could not parse latest release version: {LatestReleaseTagName}",
                    latestRelease.TagName
                );
                return null;
            }

            var currentVersion = Version.Parse(Shared.FileVersion);

            if (latestVersion > currentVersion)
            {
                if (FindExecutableAsset(latestRelease) == null)
                {
                    _logger.LogWarning("No update executable (.exe) found in the latest release");
                    return null;
                }

                var latestVersionStr = latestVersion.ToString();
                _logger.LogInformation(
                    "A new version ({LatestVersion}) is available!",
                    latestVersionStr
                );

                return latestVersionStr;
            }

            _logger.LogInformation("You are running the latest version");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return null;
        }
    }

    /// <summary>Releases the underlying <see cref="HttpClient"/> resources.</summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

// Helper classes for deserializing GitHub API response
/// <summary>
///     Represents a GitHub release fetched from the API. Used internally for deserialization.
/// </summary>
public class GitHubRelease
{
    /// <summary>Gets or sets the release tag name (e.g., "v1.2.0").</summary>
    [JsonProperty("tag_name")]
    public required string TagName { get; set; }

    /// <summary>Gets or sets the list of assets attached to this release.</summary>
    [JsonProperty("assets")]
    public required List<GitHubAsset> Assets { get; set; }

    /// <summary>Gets or sets the release body text (release notes).</summary>
    [JsonProperty("body")]
    public required string Body { get; set; }
}

/// <summary>Represents a downloadable asset attached to a GitHub release.</summary>
public class GitHubAsset
{
    /// <summary>Gets or sets the file name of the asset.</summary>
    [JsonProperty("name")]
    public required string Name { get; set; }

    /// <summary>Gets or sets the browser-download URL for the asset.</summary>
    [JsonProperty("browser_download_url")]
    public required string BrowserDownloadUrl { get; set; }
}
