using Microsoft.Extensions.Logging;
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

        Log.LogInformation("Starting download from {Url} to {FilePath}", url, filePath);

        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url).ConfigureAwait(false);

            Log.LogDebug("Received HTTP {StatusCode} from {Url}", response.StatusCode, url);

            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs).ConfigureAwait(false);

            var length = fs.Length;
            Log.LogInformation("Successfully downloaded {Length} bytes from {Url} to {FilePath}", length, url,
                filePath);

            return (true, filePath);
        }
        catch (HttpRequestException ex)
        {
            Log.LogError(ex, "Network error while downloading {Url}", url);
            return (false, null);
        }
        catch (IOException ex)
        {
            Log.LogError(ex, "File I/O error while saving {FilePath}", filePath);
            return (false, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.LogError(ex, "Access denied when writing to {FilePath}", filePath);
            return (false, null);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Unexpected error downloading {Url} to {FilePath}", url, filePath);
            return (false, null);
        }
    }
}