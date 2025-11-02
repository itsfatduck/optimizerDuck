using ConsoleInk;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using optimizerDuck.Core;
using optimizerDuck.Core.Services;
using optimizerDuck.UI;
using optimizerDuck.UI.Components;
using optimizerDuck.UI.Logger;
using Spectre.Console;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;

namespace optimizerDuck.src.Core.Services;

public class UpdateService
{
    private const string Owner = "itsfatduck";
    private const string Repo = "optimizerDuck";
    private static readonly HttpClient HttpClient;
    private static readonly ILogger Log = Logger.CreateLogger<UpdateService>();

    static UpdateService()
    {
        HttpClient = new HttpClient();
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("optimizerDuck", "1.0"));
    }

    public async Task CheckForUpdatesAsync()
    {
        Log.LogInformation("Checking for updates...");

        try
        {
            var response = await HttpClient.GetStringAsync($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
            var latestRelease = JsonConvert.DeserializeObject<GitHubRelease>(response);

            if (latestRelease == null || string.IsNullOrEmpty(latestRelease.TagName))
            {
                Log.LogWarning("Could not retrieve latest release information.");
                return;
            }

            // "v1.1.0" -> "1.1.0"
            var latestVersionStr = latestRelease.TagName.TrimStart('v');

            // "1.1.0-fix" -> "1.1.0"
            var dashIndex = latestVersionStr.IndexOf('-');
            if (dashIndex != -1)
                latestVersionStr = latestVersionStr[..dashIndex];

            // Parse version
            if (!Version.TryParse(latestVersionStr, out var latestVersion))
            {
                Log.LogWarning($"Could not parse latest release version: {latestRelease.TagName}");
                return;
            }

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version
                                 ?? new Version(0, 0, 0);

            if (latestVersion > currentVersion)
            {
                var asset = latestRelease.Assets?
                    .FirstOrDefault(a =>
                        a.Name.StartsWith("optimizerDuck", StringComparison.OrdinalIgnoreCase)
                        && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                if (asset == null)
                {
                    Log.LogWarning("No update executable (.exe) found in the latest release.");
                    return;
                }


                Log.LogInformation($"A new version ({latestVersion}) is available!");

                if (!string.IsNullOrWhiteSpace(latestRelease.Body))
                {
                    Log.LogDebug($"Release notes: {latestRelease.Body}");

                    var options = new MarkdownRenderOptions
                    {
                        UseHyperlinks = true,
                        EnableColors = true,
                        Theme = ConsoleTheme.Default
                    };
                    MarkdownConsole.Render(latestRelease.Body, Console.Out, options);
                }

                if (PromptDialog.Warning(
                    "Update Available",
                    $"""
                    A new version [{Theme.Primary}]{latestVersionStr}[/] is ready to install.
                    Updating ensures you get the latest features and fixes.
        
                    Would you like to download and install it now?
                    """,
                    new PromptOption("Yes, update now", Theme.Success),
                    new PromptOption("Not now", Theme.Warning, () => false)))
                {
                    await DownloadAndApplyUpdate(asset);
                }
            }
            else
            {
                Log.LogInformation("You are running the latest version.");
            }
        }
        catch (Exception ex)
        {
            Log.LogError(ex, $"Error checking for updates: [{Theme.Error}]{ex.Message}[/]");
        }
    }

    private async Task DownloadAndApplyUpdate(GitHubAsset asset)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), asset.Name);

        await AnsiConsole.Progress().StartAsync(async ctx =>
        {
            Log.LogInformation("Downloading update from {DownloadUrl} to {TempPath}", asset.BrowserDownloadUrl, tempPath);
            var task = ctx.AddTask($"Downloading [{Theme.Primary}]{asset.Name}[/]");

            using var response = await HttpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            task.MaxValue = totalBytes;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long downloadedBytes = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloadedBytes += bytesRead;
                task.Value = downloadedBytes;
            }
        });

        Log.LogInformation("Download complete.");
        LaunchUpdater(tempPath);
    }

    private void LaunchUpdater(string exePath)
    {
        Log.LogInformation("Starting update...");
        var currentProcess = Process.GetCurrentProcess();
        Log.LogDebug("Current process ID: {Pid}", currentProcess.Id);
        Log.LogDebug("New executable path: {ExePath}", exePath);
        Log.LogDebug("Current (old) executable path: {OldExePath}", Defaults.ExePath);

        string script = $"""
                     Write-Host "Updater script started."
                     Write-Host "Waiting for main process ({currentProcess.Id}) to exit..."
                     Wait-Process -Id {currentProcess.Id}
                     Write-Host "Main process exited."

                     Write-Host "Replacing old executable..."
                     Copy-Item -Path {exePath} -Destination {Defaults.ExePath} -Force
                     Write-Host "Replacement complete."

                     Write-Host "Starting updated application..."
                     Start-Process -FilePath {Defaults.ExePath}

                     Write-Host "Cleaning up..."
                     Remove-Item -Path {exePath} -Force
                     Write-Host "Updater script finished."
                     """;

        var psi = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NonInteractive -NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand {ShellService.EncodePowerShellCommand(script)}",
                UseShellExecute = true, // start as a separate process via the shell so it doesn't inherit debugger/std handles
                CreateNoWindow = true
            }
        };

        psi.Start();
        Environment.Exit(0);
    }

}

// Helper classes for deserializing GitHub API response
public class GitHubRelease
{
    [JsonProperty("tag_name")]
    public required string TagName { get; set; }

    [JsonProperty("assets")]
    public required List<GitHubAsset> Assets { get; set; }

    [JsonProperty("body")]
    public required string Body { get; set; }
}

public class GitHubAsset
{
    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("browser_download_url")]
    public required string BrowserDownloadUrl { get; set; }
}
