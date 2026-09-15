using System.IO;
using System.Reflection;

namespace optimizerDuck.Common.Helpers;

/// <summary>
/// Extracts embedded resources from the optimizerDuck.Resources.Embedded namespace.
/// </summary>
public static class EmbeddedResourceHelper
{
    private const string ResourceNamespace = "optimizerDuck.Resources.Embedded";

    /// <summary>
    /// Extracts an embedded resource to the specified output path.
    /// </summary>
    /// <param name="relativePath">
    ///     Relative path inside the Embedded namespace, for example "Icons/blank.ico".
    /// </param>
    /// <param name="outputPath">Full path where the resource will be extracted.</param>
    /// <param name="overwrite">Whether to overwrite the file if it already exists.</param>
    /// <returns>
    ///     <see langword="true" /> if extraction succeeded; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryExtract(string relativePath, string outputPath, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(outputPath))
        {
            return false;
        }

        try
        {
            // Normalize the relative path to use dots as separators for resource names
            var normalizedPath = relativePath.Replace('/', '.').Replace('\\', '.');
            var fullResourceName = $"{ResourceNamespace}.{normalizedPath}";

            var assembly = Assembly.GetExecutingAssembly();

            if (!ResourceExists(assembly, fullResourceName))
            {
                return false;
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            if (File.Exists(outputPath) && !overwrite)
            {
                return true;
            }

            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            if (stream == null)
            {
                return false;
            }

            using var fileStream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 8192,
                useAsync: false
            );

            stream.CopyTo(fileStream);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if an embedded resource exists.
    /// </summary>
    /// <param name="relativePath">Relative path within the Embedded namespace.</param>
    /// <returns>
    ///     <see langword="true" /> if the resource exists; otherwise <see langword="false" />.
    /// </returns>
    public static bool Exists(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalizedPath = relativePath.Replace('/', '.').Replace('\\', '.');
        var fullResourceName = $"{ResourceNamespace}.{normalizedPath}";

        return ResourceExists(Assembly.GetExecutingAssembly(), fullResourceName);
    }

    /// <summary>
    /// Gets all available embedded resources in the optimizerDuck.Resources.Embedded namespace.
    /// </summary>
    /// <returns>Enumerable of resource names with the namespace prefix removed.</returns>
    private static readonly string[] _cachedResourceNames = Assembly
        .GetExecutingAssembly()
        .GetManifestResourceNames();

    private static readonly HashSet<string> _cachedResourceSet = new(
        _cachedResourceNames,
        StringComparer.Ordinal
    );

    public static IEnumerable<string> GetAvailableResources()
    {
        var prefix = $"{ResourceNamespace}.";

        return _cachedResourceNames
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => name.Substring(prefix.Length));
    }

    private static bool ResourceExists(Assembly assembly, string resourceName)
    {
        return _cachedResourceSet.Contains(resourceName);
    }
}
