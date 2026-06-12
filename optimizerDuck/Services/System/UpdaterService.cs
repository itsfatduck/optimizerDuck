using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Services;

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
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("optimizerDuck", "1.0")
        );
        _logger = logger;
    }

    /// <summary>Checks the GitHub API for a newer version of the application.</summary>
    /// <returns>A tuple where <c>Result</c> is <see langword="true"/> if a newer version exists, and <c>Version</c> is the latest version string.</returns>
    public async Task<(bool Result, string? Version)> CheckForUpdatesAsync()
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
                return (false, null);
            }

            // "v1.1.0" -> "1.1.0"
            var latestVersionStr = latestRelease.TagName.TrimStart('v');

            // "1.1.0-fix" -> "1.1.0"
            var preReleaseSeparatorIndex = latestVersionStr.IndexOf('-');
            if (preReleaseSeparatorIndex != -1)
                latestVersionStr = latestVersionStr[..preReleaseSeparatorIndex];

            _logger.LogDebug(
                "Latest release version: {LatestReleaseTagName}",
                latestRelease.TagName
            );

            // Parse version
            if (!Version.TryParse(latestVersionStr, out var latestVersion))
            {
                _logger.LogWarning(
                    "Could not parse latest release version: {LatestReleaseTagName}",
                    latestRelease.TagName
                );
                return (false, null);
            }

            var currentVersion = Version.Parse(Shared.FileVersion);

            if (latestVersion > currentVersion)
            {
                var updateExecutableAsset = latestRelease.Assets.FirstOrDefault(a =>
                    a.Name.StartsWith("optimizerDuck", StringComparison.OrdinalIgnoreCase)
                    && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                );

                if (updateExecutableAsset == null)
                {
                    _logger.LogWarning("No update executable (.exe) found in the latest release");
                    return (false, null);
                }

                _logger.LogInformation(
                    "A new version ({LatestVersion}) is available!",
                    latestVersionStr
                );

                return (true, latestVersionStr);
            }

            _logger.LogInformation("You are running the latest version");
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return (false, null);
        }
    }

    /// <summary>Releases the underlying <see cref="HttpClient"/> resources.</summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

// Helper classes for deserializing GitHub API response
/// <summary>Represents a GitHub release fetched from the API. Used internally for deserialization.</summary>
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
