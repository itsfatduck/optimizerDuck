using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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

            var latestVersionStr = latestRelease.TagName.TrimStart('v');
            if (Version.TryParse(latestVersionStr, out var latestVersion))
            {
                Log.LogWarning($"Could not parse latest release version: {latestRelease.TagName}");
                return;
            }

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            if (true)
            {
                Log.LogInformation($"[green]A new version ({latestVersion}) is available![/]");
                var asset = latestRelease.Assets?.FirstOrDefault(a => a.Name.StartsWith("optimizerDuck", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".exe"));

                if (asset == null)
                {
                    Log.LogWarning("No update executable (.exe) found in the latest release.");
                    return;
                }

                if (PromptDialog.Warning("Found New Update",
                        $"""
                        Found new update version: [{Theme.Primary}]{latestVersionStr}[/].
                        Do you want to download and install it now?
                        """,
                        new PromptOption("Yes", Theme.Success),
                        new PromptOption("Not now", Theme.Warning, () => false)))
                    await DownloadAndApplyUpdate(asset);

            }
            else
            {
                AnsiConsole.MarkupLine("[green]You are running the latest version.[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error checking for updates: {ex.Message}[/]");
        }
    }

    private async Task DownloadAndApplyUpdate(GitHubAsset asset)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), asset.Name);

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[cyan]Downloading {asset.Name}[/]");
                using var response = await HttpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                task.MaxValue = totalBytes;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long downloadedBytes = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    task.Value = downloadedBytes;
                }
            });

        AnsiConsole.MarkupLine("[green]Download complete.[/] [grey]Starting update...[/]");
        LaunchUpdater(tempPath);
    }

    private void LaunchUpdater(string zipPath)
    {
        var currentProcess = Process.GetCurrentProcess();
        var exePath = currentProcess.MainModule!.FileName;
        var appDirectory = AppContext.BaseDirectory;
        var exeName = Path.GetFileName(exePath);

        string script = $"""
                         param (
                             [string]$Pid,
                             [string]$ZipPath,
                             [string]$ExtractPath,
                             [string]$ExeName
                         )
                         
                         Write-Host "Updater script started."
                         Write-Host "Waiting for main process ($Pid) to exit..."
                         Wait-Process -Id $Pid
                         Write-Host "Main process exited."
                         
                         Write-Host "Extracting update from '$ZipPath' to '$ExtractPath'..."
                         Expand-Archive -Path $ZipPath -DestinationPath $ExtractPath -Force
                         Write-Host "Extraction complete."
                         
                         Write-Host "Cleaning up..."
                         Remove-Item -Path $ZipPath
                         Write-Host "Update package removed."
                         
                         $exePath = Join-Path -Path $ExtractPath -ChildPath $ExeName
                         Write-Host "Restarting application: '$exePath'..."
                         Start-Process -FilePath $exePath
                         Write-Host "Updater script finished."
                         """;

        ShellService.PowerShell($"{script} -Pid {currentProcess.Id} -ZipPath \"{zipPath}\" -ExtractPath \"{appDirectory}\" -ExeName \"{exeName}\"");

        Environment.Exit(0);
    }
}

// Helper classes for deserializing GitHub API response
public class GitHubRelease
{
    [JsonProperty("tag_name")]
    public string TagName { get; set; }

    [JsonProperty("assets")]
    public List<GitHubAsset> Assets { get; set; }
}

public class GitHubAsset
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("browser_download_url")]
    public string BrowserDownloadUrl { get; set; }
}
