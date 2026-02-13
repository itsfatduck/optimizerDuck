using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Services;

public class UpdaterService
{
    private const string Owner = "itsfatduck";
    private const string Repo = "optimizerDuck";
    
    public const string LatestReleaseUrl = $"https://github.com/{Owner}/{Repo}/releases/latest";
    
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public UpdaterService(ILogger<UpdaterService> logger)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("optimizerDuck", "1.0"));
        _logger = logger;
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        _logger.LogInformation("Checking for updates (Current version: {CurrentVersion})",
            Shared.FileVersion);

        try
        {
            var response =
                await _httpClient.GetStringAsync($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
            var latestRelease = JsonConvert.DeserializeObject<GitHubRelease>(response);

            if (latestRelease == null || string.IsNullOrEmpty(latestRelease.TagName))
            {
                _logger.LogWarning("Could not retrieve latest release information");
                return false;
            }

            // "v1.1.0" -> "1.1.0"
            var latestVersionStr = latestRelease.TagName.TrimStart('v');

            // "1.1.0-fix" -> "1.1.0"
            var preReleaseSeparatorIndex = latestVersionStr.IndexOf('-');
            if (preReleaseSeparatorIndex != -1)
                latestVersionStr = latestVersionStr[..preReleaseSeparatorIndex];

            _logger.LogDebug("Latest release version: {LatestReleaseTagName}", latestRelease.TagName);

            // Parse version
            if (!Version.TryParse(latestVersionStr, out var latestVersion))
            {
                _logger.LogWarning("Could not parse latest release version: {LatestReleaseTagName}", latestRelease.TagName);
                return false;
            }

            var currentVersion = Version.Parse(Shared.FileVersion);

            if (latestVersion > currentVersion)
            {
                var updateExecutableAsset = latestRelease.Assets
                    .FirstOrDefault(a =>
                        a.Name.StartsWith("optimizerDuck", StringComparison.OrdinalIgnoreCase)
                        && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                if (updateExecutableAsset == null)
                {
                    _logger.LogWarning("No update executable (.exe) found in the latest release");
                    return false;
                }

                _logger.LogInformation("A new version ({LatestVersion}) is available!", latestVersionStr);

                return true;
            }
            else
            {
                _logger.LogInformation("You are running the latest version");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking for updates");
            return false;
        }
    }
}

// Helper classes for deserializing GitHub API response
public class GitHubRelease
{
    [JsonProperty("tag_name")] public required string TagName { get; set; }

    [JsonProperty("assets")] public required List<GitHubAsset> Assets { get; set; }

    [JsonProperty("body")] public required string Body { get; set; }
}

public class GitHubAsset
{
    [JsonProperty("name")] public required string Name { get; set; }

    [JsonProperty("browser_download_url")] public required string BrowserDownloadUrl { get; set; }
}