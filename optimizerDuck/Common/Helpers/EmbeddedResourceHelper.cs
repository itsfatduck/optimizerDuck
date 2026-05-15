using System.IO;
using System.Reflection;

namespace optimizerDuck.Common.Helpers;

/// <summary>
/// Helper class for extracting embedded resources from the optimizerDuck.Resources.Embedded namespace.
/// </summary>
public static class EmbeddedResourceHelper
{
    private const string ResourceNamespace = "optimizerDuck.Resources.Embedded";

    /// <summary>
    /// Extracts an embedded resource to the specified output path.
    /// </summary>
    /// <param name="relativePath">Relative path within the Embedded namespace (e.g., "Icons/blank.ico").</param>
    /// <param name="outputPath">Full path where the resource will be extracted.</param>
    /// <param name="overwrite">Whether to overwrite the file if it already exists.</param>
    /// <returns>True if extraction succeeded, false otherwise.</returns>
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

            // Check if the resource exists
            if (!ResourceExists(assembly, fullResourceName))
            {
                return false;
            }

            // Create output directory if it doesn't exist
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Skip extraction if file exists and overwrite is disabled
            if (File.Exists(outputPath) && !overwrite)
            {
                return true;
            }

            // Extract the resource
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
    /// <returns>True if the resource exists, false otherwise.</returns>
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
    public static IEnumerable<string> GetAvailableResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var prefix = $"{ResourceNamespace}.";

        return assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => name.Substring(prefix.Length));
    }

    /// <summary>
    /// Checks if a specific resource exists in the assembly.
    /// </summary>
    private static bool ResourceExists(Assembly assembly, string resourceName)
    {
        return assembly.GetManifestResourceNames().Contains(resourceName);
    }
}
