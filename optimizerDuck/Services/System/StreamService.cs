using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Services.System;

public class StreamService(ILogger<StreamService> logger) : IDisposable
{
    private HttpClient? _client;

    /// <summary>Downloads a file from the specified URL and saves it to the local downloads directory.</summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="fileName">The target file name (not path) to save as.</param>
    /// <returns>A tuple where <c>Success</c> indicates whether the download completed, and <c>FilePath</c> is the full local path on success.</returns>
    /// <example>
    /// <code language="csharp">
    /// var (success, path) = await streamService.TryDownloadAsync("https://example.com/file.zip", "file.zip");
    /// </code>
    /// </example>
    public async Task<(bool Success, string? FilePath)> TryDownloadAsync(
        string url,
        string fileName
    )
    {
        var filePath = Path.Combine(Shared.DownloadsDirectory, fileName);

        logger.LogInformation("Starting download from {Url} to {FilePath}", url, filePath);

        try
        {
            Directory.CreateDirectory(Shared.DownloadsDirectory);
            _client ??= new HttpClient();
            using var response = await _client.GetAsync(url).ConfigureAwait(false);

            logger.LogDebug("Received HTTP {StatusCode} from {Url}", response.StatusCode, url);

            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None
            );
            await response.Content.CopyToAsync(fs).ConfigureAwait(false);

            var length = fs.Length;
            logger.LogInformation(
                "Successfully downloaded {Length} bytes from {Url} to {FilePath}",
                length,
                url,
                filePath
            );

            return (true, filePath);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error while downloading {Url}", url);
            return (false, null);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "File I/O error while saving {FilePath}", filePath);
            return (false, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Access denied when writing to {FilePath}", filePath);
            return (false, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error downloading {Url} to {FilePath}", url, filePath);
            return (false, null);
        }
    }

    /// <summary>Releases the underlying <see cref="HttpClient"/> resources.</summary>
    public void Dispose()
    {
        _client?.Dispose();
    }
}
