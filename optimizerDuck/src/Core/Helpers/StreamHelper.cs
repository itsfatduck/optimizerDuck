using Microsoft.Extensions.Logging;
using optimizerDuck.src.Core;
using optimizerDuck.UI.Logger;

namespace optimizerDuck.Core.Helpers;

public static class StreamHelper
{
    private static readonly ILogger Log = Logger.CreateLogger(typeof(StreamHelper));

    public static async Task<(bool Success, string? FilePath)> TryDownloadAsync(
        string url,
        string fileName)
    {
        var filePath = Path.Combine(Defaults.ResourcesPath, fileName);

        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs).ConfigureAwait(false);

            Log.LogInformation("Downloaded file from {Url} to {FilePath}", url, filePath);
            return (true, filePath);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Failed to download file from {Url} to {FilePath}", url, filePath);
            return (false, null);
        }
    }
}