using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using optimizerDuck.Resources.Languages;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Common.Helpers;

/// <summary>
///     Provides shared logic for opening source files on GitHub, with built-in caching
///     of raw content to avoid repeated network requests.
/// </summary>
public static class GitHubSourceHelper
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly ConcurrentDictionary<
        string,
        Lazy<Task<(string Content, DateTime FetchedAt)>>
    > SourceCache = new();
    private static readonly TimeSpan SourceCacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Opens the GitHub source file for the given type at the class definition line.
    /// </summary>
    /// <param name="ownerType">The type that owns the source file (e.g., the category class).</param>
    /// <param name="className">The class name to find within the source file.</param>
    /// <param name="baseClassPattern">Optional base class pattern to search for (e.g., "BaseCustomizeSetting").</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="snackbarService">Optional snackbar service for user-facing error notifications.</param>
    public static async Task OpenSourceOnGitHubAsync(
        Type ownerType,
        string className,
        string? baseClassPattern = null,
        ILogger? logger = null,
        ISnackbarService? snackbarService = null
    )
    {
        var fileName = ownerType.Name;
        var namespacePath = (ownerType.Namespace ?? string.Empty).Replace('.', '/');
        var relativePath = $"{namespacePath}/{fileName}.cs";
        var url = $"{Shared.GitHubRepoURL}/blob/master/{relativePath}";

        // Fetch source from GitHub raw content to find the class line number
        try
        {
            var rawUrl =
                $"https://raw.githubusercontent.com/itsfatduck/optimizerDuck/master/{relativePath}";

            var cached = SourceCache.GetOrAdd(rawUrl, CreateSourceCacheEntry);
            var cachedSource = await cached.Value;

            if (DateTime.UtcNow - cachedSource.FetchedAt >= SourceCacheTtl)
            {
                var refreshed = CreateSourceCacheEntry(rawUrl);
                SourceCache[rawUrl] = refreshed;
                cachedSource = await refreshed.Value;
            }

            var source = cachedSource.Content;

            // Use regex with word boundaries to avoid false-positive substring matches
            var classNameEscaped = Regex.Escape(className);
            var pattern =
                baseClassPattern != null
                    ? $@"class\s+{classNameEscaped}\s*:\s*{Regex.Escape(baseClassPattern)}\b"
                    : $@"class\s+{classNameEscaped}\b";

            var lineIndex = -1;
            var lines = source.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], pattern, RegexOptions.IgnoreCase))
                {
                    lineIndex = i;
                    break;
                }
            }

            if (lineIndex >= 0)
                url += $"#L{lineIndex + 1}";
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Could not fetch source to find line number for {Class}",
                className
            );
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to open GitHub URL: {Url}", url);
            snackbarService?.Show(
                Translations.Snackbar_OpenLinkFailed_Title,
                Translations.Snackbar_OpenLinkFailed_Message,
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }
    }

    private static Lazy<Task<(string Content, DateTime FetchedAt)>> CreateSourceCacheEntry(
        string rawUrl
    )
    {
        return new Lazy<Task<(string Content, DateTime FetchedAt)>>(async () =>
            (await HttpClient.GetStringAsync(rawUrl), DateTime.UtcNow)
        );
    }
}
