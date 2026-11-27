using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.Common.Helpers;

/// <summary>
///     Provides shared logic for opening source files on GitHub pinned to the release tag
///     matching the running version (fallback: <c>master</c>). Raw contents are cached
///     permanently per immutable ref, so repeated views cost no extra requests, and a
///     missing version tag is remembered to skip its probe on later views.
/// </summary>
public static class GitHubSourceHelper
{
    private const string MasterRef = "master";
    private const string GitHubComPrefix = "https://github.com/";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private static readonly string RepoSlug = Shared.GitHubRepoURL.StartsWith(
        GitHubComPrefix,
        StringComparison.Ordinal
    )
        ? Shared.GitHubRepoURL[GitHubComPrefix.Length..].TrimEnd('/')
        : string.Empty;

    private static readonly HttpClient HttpClient = CreateClient();

    /// <summary>
    ///     Raw file contents keyed by ref and path. Tag refs are immutable, so entries never
    ///     expire.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<Task<string>>> SourceCache = new();

    /// <summary>
    ///     Tags known missing upstream; skips one wasted probe per view until the app restarts.
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> MissingTagCache = new();

    /// <summary>
    ///     Opens the GitHub source file for the given type at the class definition line,
    ///     viewed at the release tag matching the running application version.
    /// </summary>
    /// <param name="ownerType">
    ///     The type that owns the source file, for example the category class.
    /// </param>
    /// <param name="className">The class name to find within the source file.</param>
    /// <param name="baseClassPattern">
    ///     Optional base class pattern to search for, for example "BaseCustomizeSetting".
    /// </param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="snackbarService">
    ///     Optional snackbar service for user-facing error notifications.
    /// </param>
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

        var (source, resolvedRef) = await TryGetSourceAsync(relativePath, logger);
        var url = $"{Shared.GitHubRepoURL}/blob/{resolvedRef}/{relativePath}";

        if (source != null)
        {
            var lineIndex = FindClassLineNumber(source, className, baseClassPattern);
            if (lineIndex >= 0)
                url += $"#L{lineIndex + 1}";
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to open GitHub URL: {Url}", url);
            snackbarService?.Show(
                Loc.Instance["Snackbar.OpenLinkFailed.Title"],
                Loc.Instance["Snackbar.OpenLinkFailed.Message"],
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }
    }

    /// <summary>
    ///     Maps the running file version to its release tag, for example "2.26.2" to "v2.26.2".
    /// </summary>
    internal static string? GetTagForVersion(string? FileVersion)
    {
        if (!Version.TryParse(FileVersion, out var version))
            return null;

        return version.Build >= 0
            ? $"v{version.Major}.{version.Minor}.{version.Build}"
            : $"v{version.Major}.{version.Minor}";
    }

    /// <summary>Finds the zero-based line of the class declaration, or -1 when absent.</summary>
    internal static int FindClassLineNumber(
        string source,
        string className,
        string? baseClassPattern = null
    )
    {
        var remaining = source.AsSpan();
        var classSpan = className.AsSpan();
        var baseSpan = baseClassPattern.AsSpan();
        var hasBase = baseClassPattern != null;
        var lineIndex = 0;

        while (true)
        {
            var newlineIdx = remaining.IndexOf('\n');
            ReadOnlySpan<char> line;
            if (newlineIdx >= 0)
            {
                line = remaining[..newlineIdx];
                remaining = remaining[(newlineIdx + 1)..];
            }
            else
            {
                line = remaining;
            }

            if (MatchesClassDeclarationLine(line, classSpan, baseSpan, hasBase))
                return lineIndex;

            if (newlineIdx < 0)
                break;

            lineIndex++;
        }

        return -1;
    }

    private static bool MatchesClassDeclarationLine(
        ReadOnlySpan<char> line,
        ReadOnlySpan<char> className,
        ReadOnlySpan<char> baseClassPattern,
        bool hasBase
    )
    {
        const string keyword = "class";
        var searchStart = 0;

        while (searchStart <= line.Length - keyword.Length)
        {
            var hit = line[searchStart..].IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (hit < 0)
                return false;

            var idx = searchStart + hit + keyword.Length;
            searchStart += hit + 1;

            if (idx >= line.Length || !char.IsWhiteSpace(line[idx]))
                continue;

            while (idx < line.Length && char.IsWhiteSpace(line[idx]))
                idx++;

            if (!line[idx..].StartsWith(className, StringComparison.OrdinalIgnoreCase))
                continue;

            idx += className.Length;

            if (!hasBase)
            {
                if (className.Length == 0 || !IsWordChar(className[^1]))
                    return true;

                if (idx == line.Length || !IsWordChar(line[idx]))
                    return true;

                continue;
            }

            while (idx < line.Length && char.IsWhiteSpace(line[idx]))
                idx++;

            if (idx >= line.Length || line[idx] != ':')
                continue;

            idx++;
            while (idx < line.Length && char.IsWhiteSpace(line[idx]))
                idx++;

            if (!line[idx..].StartsWith(baseClassPattern, StringComparison.OrdinalIgnoreCase))
                continue;

            idx += baseClassPattern.Length;
            if (baseClassPattern.Length == 0 || !IsWordChar(baseClassPattern[^1]))
                return true;

            if (idx == line.Length || !IsWordChar(line[idx]))
                return true;
        }

        return false;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static async Task<(string? Content, string ResolvedRef)> TryGetSourceAsync(
        string relativePath,
        ILogger? logger
    )
    {
        var tag = GetTagForVersion(Shared.FileVersion);
        if (tag != null && !MissingTagCache.ContainsKey(tag))
        {
            var pinned = await FetchAsync(BuildRawUrl(tag, relativePath), logger, tag);
            if (pinned != null)
                return (pinned, tag);
        }

        return (await FetchAsync(BuildRawUrl(MasterRef, relativePath), logger), MasterRef);
    }

    private static async Task<string?> FetchAsync(
        string rawUrl,
        ILogger? logger,
        string? tag = null
    )
    {
        var entry = SourceCache.GetOrAdd(rawUrl, CreateCacheEntry);
        try
        {
            return await entry.Value;
        }
        catch (Exception ex)
        {
            // Evict so a transient failure (or a not-yet-published tag) is retried
            // later instead of poisoning the cache entry forever.
            SourceCache.TryRemove(KeyValuePair.Create(rawUrl, entry));

            // A pinned-tag miss usually means the tag is not published (yet); remember
            // it so later views skip straight to master instead of re-probing.
            if (
                tag != null
                && ex is HttpRequestException hrex
                && hrex.StatusCode == HttpStatusCode.NotFound
            )
                MissingTagCache.TryAdd(tag, 0);

            logger?.LogWarning(ex, "Could not fetch GitHub source: {Url}", rawUrl);
            return null;
        }
    }

    private static string BuildRawUrl(string @ref, string relativePath) =>
        $"https://raw.githubusercontent.com/{RepoSlug}/{@ref}/{relativePath}";

    private static Lazy<Task<string>> CreateCacheEntry(string rawUrl) =>
        new(() => HttpClient.GetStringAsync(rawUrl));

    private static HttpClient CreateClient()
    {
        var client = HttpClientFactory.CreateClient(timeout: RequestTimeout);
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("optimizerDuck", Shared.FileVersion)
        );
        return client;
    }
}
