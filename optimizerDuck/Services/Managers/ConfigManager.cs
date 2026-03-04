using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Services.Managers;

/// <summary>
///     Manages application configuration settings stored in appsettings.json.
/// </summary>
public class ConfigManager(IConfiguration configuration, ILogger<ConfigManager> logger)
{
    /// <summary>
    ///     The path to the configuration file.
    /// </summary>
    private readonly string _configPath = Path.Combine(Shared.RootDirectory, "appsettings.json");

    /// <summary>
    ///     Semaphore for thread-safe access to configuration data.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    ///     Cached configuration data.
    /// </summary>
    private JObject _cache = new();

    /// <summary>
    ///     Initializes the configuration manager by loading settings from disk.
    /// </summary>
    public async Task InitializeAsync()
    {
        _cache = await LoadConfigAsync();
    }

    /// <summary>
    ///     Sets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key (e.g., "App:Language").</param>
    /// <param name="value">The value to set.</param>
    public async Task SetAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            var parts = key.Split(':');
            var current = _cache;

            for (var i = 0; i < parts.Length - 1; i++)
                current = GetOrCreateObjectIgnoreCase(current, parts[i]);

            SetValueIgnoreCase(current, parts[^1], value);
            await SaveConfigAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Removes a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key to remove.</param>
    public async Task RemoveAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            var parts = key.Split(':');
            var current = _cache;

            for (var i = 0; i < parts.Length - 1; i++)
            {
                var next = GetTokenIgnoreCase(current, parts[i]);
                if (next is not JObject nextObj) return;
                current = nextObj;
            }

            RemoveIgnoreCase(current, parts[^1]);
            await SaveConfigAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Removes a configuration value by key (synchronous wrapper).
    /// </summary>
    /// <param name="key">The configuration key to remove.</param>
    public void Remove(string key)
    {
        _ = RemoveAsync(key);
    }

    /// <summary>
    ///     Loads configuration from disk.
    /// </summary>
    /// <returns>The loaded configuration as a JObject.</returns>
    private async Task<JObject> LoadConfigAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);

            if (!File.Exists(_configPath))
                return new JObject();

            var content = await File.ReadAllTextAsync(_configPath);

            return string.IsNullOrWhiteSpace(content)
                ? new JObject()
                : JsonConvert.DeserializeObject<JObject>(content) ?? new JObject();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invalid config file, resetting");
            return new JObject();
        }
    }

    /// <summary>
    ///     Saves the current configuration to disk.
    /// </summary>
    private async Task SaveConfigAsync()
    {
        await File.WriteAllTextAsync(
            _configPath,
            _cache.ToString(Formatting.Indented));

        (configuration as IConfigurationRoot)?.Reload();
    }

    /// <summary>
    ///     Gets a token by key (case-insensitive).
    /// </summary>
    /// <param name="obj">The JObject to search.</param>
    /// <param name="key">The key to find.</param>
    /// <returns>The matching token, or null if not found.</returns>
    private static JToken? GetTokenIgnoreCase(JObject obj, string key)
    {
        var prop = obj.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
        return prop?.Value;
    }

    /// <summary>
    ///     Gets or creates an object by key (case-insensitive).
    /// </summary>
    /// <param name="current">The current JObject.</param>
    /// <param name="key">The key to find or create.</param>
    /// <returns>The existing or newly created JObject.</returns>
    private static JObject GetOrCreateObjectIgnoreCase(JObject current, string key)
    {
        JProperty? first = null;
        var extraNames = new List<string>();

        foreach (var p in current.Properties())
            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                if (first == null) first = p;
                else extraNames.Add(p.Name);
            }

        foreach (var name in extraNames) current.Remove(name);

        if (first?.Value is JObject existing)
            return existing;

        var created = new JObject();

        if (first != null)
        {
            first.Value = created;
            return created;
        }

        current[key] = created;
        return created;
    }

    /// <summary>
    ///     Sets a value by key (case-insensitive).
    /// </summary>
    /// <param name="current">The current JObject.</param>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to set.</param>
    private static void SetValueIgnoreCase(JObject current, string key, string value)
    {
        // Remove all case-insensitive matches
        var toRemove = current.Properties()
            .Where(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        foreach (var name in toRemove)
            current.Remove(name);

        // Add with correct casing
        current[key] = value;
    }

    /// <summary>
    ///     Removes a value by key (case-insensitive).
    /// </summary>
    /// <param name="current">The current JObject.</param>
    /// <param name="key">The key to remove.</param>
    private static void RemoveIgnoreCase(JObject current, string key)
    {
        var toRemove = new List<string>();
        foreach (var p in current.Properties())
            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
                toRemove.Add(p.Name);

        foreach (var name in toRemove) current.Remove(name);
    }

    /// <summary>
    ///     Validates and ensures the config file exists and is valid JSON.
    /// </summary>
    public static void ValidateConfig()
    {
        var configPath = Path.Combine(Shared.RootDirectory, "appsettings.json");

        // Rewrite config file if it's empty
        if (!File.Exists(configPath) || string.IsNullOrWhiteSpace(File.ReadAllText(configPath)))
            File.WriteAllText(configPath, "{}");

        // Rewrite config file if it's invalid JSON
        try
        {
            JsonConvert.DeserializeObject<JObject>(File.ReadAllText(configPath));
        }
        catch
        {
            File.WriteAllText(configPath, "{}");
        }
    }
}