using System.IO;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Configuration;
using optimizerDuck.Domain.Serialization;

namespace optimizerDuck.Services.Configuration;

/// <summary>
///     Manages application configuration settings stored in appsettings.json.
/// </summary>
public class ConfigManager(IConfiguration configuration, ILogger<ConfigManager> logger)
{
    private readonly string _configPath = Path.Combine(Shared.RootDirectory, "appsettings.json");

    private readonly SemaphoreSlim _lock = new(1, 1);

    private JsonObject _cache = new();

    /// <summary>
    ///     Initializes the configuration manager by loading settings from disk.
    /// </summary>
    public async Task InitializeAsync()
    {
        _cache = await LoadConfigAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Seeds the configuration cache with an already validated <see cref="JsonObject" /> so
    ///     startup does not read and parse <c>appsettings.json</c> twice.
    /// </summary>
    internal void InitializeFromCache(JsonObject cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        logger.LogInformation("Loaded config with {Count} top-level sections", cache.Count);
    }

    /// <summary>
    ///     Ensures all default configuration keys exist. Adds missing keys with default values and
    ///     logs them.
    /// </summary>
    public async Task EnsureDefaultsAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var defaults = new AppSettings();
            var addedKeys = new List<string>();

            var properties = typeof(AppSettings).GetProperties();
            foreach (var prop in properties)
            {
                if (
                    prop.PropertyType.IsClass
                    && !prop.PropertyType.IsPrimitive
                    && prop.PropertyType != typeof(string)
                )
                {
                    var nestedObj = prop.GetValue(defaults);
                    if (nestedObj != null)
                    {
                        var nestedProps = prop.PropertyType.GetProperties();
                        foreach (var nestedProp in nestedProps)
                        {
                            var key = $"{prop.Name}:{nestedProp.Name}";
                            var value = nestedProp.GetValue(nestedObj);
                            if (value == null)
                                continue;

                            var existsFlat = GetTokenIgnoreCase(_cache, key) != null;
                            var existsNested = CheckNestedExists(
                                _cache,
                                prop.Name,
                                nestedProp.Name
                            );

                            if (!existsFlat && !existsNested)
                            {
                                EnsureNestedValue(
                                    _cache,
                                    prop.Name,
                                    nestedProp.Name,
                                    value.ToString() ?? ""
                                );
                                addedKeys.Add($"{key} = {value}");
                            }
                        }
                    }
                }
            }

            if (addedKeys.Count > 0)
            {
                CleanupDuplicateKeys(_cache);
                await SaveConfigAsync().ConfigureAwait(false);
                logger.LogInformation(
                    "Added {Count} missing config keys: {Keys}",
                    addedKeys.Count,
                    string.Join(", ", addedKeys)
                );
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool CheckNestedExists(JsonObject root, string section, string key)
    {
        var sectionToken = GetTokenIgnoreCase(root, section);
        if (sectionToken is not JsonObject sectionObj)
            return false;
        return GetTokenIgnoreCase(sectionObj, key) != null;
    }

    private static void EnsureNestedValue(JsonObject root, string section, string key, string value)
    {
        var sectionObj = GetOrCreateObjectIgnoreCase(root, section);
        SetValueIgnoreCase(sectionObj, key, value);
    }

    /// <summary>
    ///     Cleans up duplicate flat keys that also exist in nested format.
    /// </summary>
    private static void CleanupDuplicateKeys(JsonObject root)
    {
        var properties = typeof(AppSettings).GetProperties();
        var flatKeysToRemove = new List<string>();

        foreach (var prop in properties)
        {
            if (
                prop.PropertyType.IsClass
                && !prop.PropertyType.IsPrimitive
                && prop.PropertyType != typeof(string)
            )
            {
                var nestedProps = prop.PropertyType.GetProperties();
                foreach (var nestedProp in nestedProps)
                {
                    var flatKey = $"{prop.Name}:{nestedProp.Name}";
                    var sectionToken = GetTokenIgnoreCase(root, prop.Name);
                    if (sectionToken is JsonObject)
                    {
                        if (GetTokenIgnoreCase(root, flatKey) != null)
                            flatKeysToRemove.Add(flatKey);
                    }
                }
            }
        }

        foreach (var key in flatKeysToRemove)
            root.Remove(key);
    }

    /// <summary>
    ///     Sets a configuration value using a strongly typed property expression.
    /// </summary>
    /// <typeparam name="T">The type of the property being updated.</typeparam>
    /// <param name="property">
    ///     An expression that identifies the configuration property to update, such as
    ///     <c>x => x.App.Language</c>.
    /// </param>
    /// <param name="value">The value to assign to the specified configuration property.</param>
    /// <example>
    ///     <code>
    ///     await configManager.SetAsync(x => x.App.Language, "en");
    ///     </code>
    /// </example>
    public async Task SetAsync<T>(Expression<Func<AppSettings, T>> property, T value)
    {
        Expression expression = property.Body;
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            expression = unary.Operand;
        }

        if (expression is not MemberExpression member)
            throw new ArgumentException(
                $"The property expression '{property}' must be a simple member access expression such as 'x => x.App.Language'.",
                nameof(property)
            );
        // Build key from property path (e.g., App.Language -> "App:Language")
        var keyParts = new List<string>();
        var expr = member;
        while (expr != null)
        {
            keyParts.Insert(0, expr.Member.Name);
            if (expr.Expression is MemberExpression next)
                expr = next;
            else
                break;
        }
        var key = string.Join(":", keyParts);

        await SetAsync(key, value?.ToString() ?? "").ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key (e.g., "App:Language").</param>
    /// <param name="value">The value to set.</param>
    public async Task SetAsync(string key, string value)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var parts = key.Split(':');
            var current = _cache;

            for (var i = 0; i < parts.Length - 1; i++)
                current = GetOrCreateObjectIgnoreCase(current, parts[i]);

            SetValueIgnoreCase(current, parts[^1], value);
            await SaveConfigAsync().ConfigureAwait(false);
            logger.LogInformation("Config key {Key} set to {Value}", key, value);
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
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var parts = key.Split(':');
            var current = _cache;

            for (var i = 0; i < parts.Length - 1; i++)
            {
                var next = GetTokenIgnoreCase(current, parts[i]);
                if (next is not JsonObject nextObj)
                    return;
                current = nextObj;
            }

            RemoveIgnoreCase(current, parts[^1]);
            await SaveConfigAsync().ConfigureAwait(false);
            logger.LogInformation("Config key {Key} removed", key);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     Loads configuration from disk.
    /// </summary>
    /// <returns>The loaded configuration as a <see cref="JsonObject" />.</returns>
    private async Task<JsonObject> LoadConfigAsync()
    {
        try
        {
            var configDir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(configDir))
                Directory.CreateDirectory(configDir);

            if (!File.Exists(_configPath))
            {
                logger.LogInformation("Config file not found, starting with empty config");
                return new JsonObject();
            }

            var content = await File.ReadAllTextAsync(_configPath).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogInformation("Config file is empty, starting with empty config");
                return new JsonObject();
            }

            if (JsonNode.Parse(content) is not JsonObject config)
            {
                logger.LogInformation("Config file is invalid, starting with empty config");
                return new JsonObject();
            }

            var propertyCount = config.Count;
            logger.LogInformation("Loaded config with {Count} top-level sections", propertyCount);
            return config;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invalid config file, resetting");
            return new JsonObject();
        }
    }

    /// <summary>
    ///     Saves the current configuration to disk.
    /// </summary>
    private async Task SaveConfigAsync()
    {
        await File.WriteAllTextAsync(
                _configPath,
                _cache.ToJsonString(AppJsonContext.IndentedOptions)
            )
            .ConfigureAwait(false);

        (configuration as IConfigurationRoot)?.Reload();
    }

    /// <summary>
    ///     Gets a node by key (case-insensitive).
    /// </summary>
    private static JsonNode? GetTokenIgnoreCase(JsonObject obj, string key)
    {
        foreach (var kvp in obj)
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        return null;
    }

    /// <summary>
    ///     Gets or creates an object by key (case-insensitive).
    /// </summary>
    private static JsonObject GetOrCreateObjectIgnoreCase(JsonObject current, string key)
    {
        string? firstKey = null;
        JsonNode? firstValue = null;
        List<string>? extraNames = null;

        foreach (var kvp in current)
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                if (firstKey == null)
                {
                    firstKey = kvp.Key;
                    firstValue = kvp.Value;
                }
                else
                {
                    (extraNames ??= []).Add(kvp.Key);
                }
            }

        if (extraNames != null)
            foreach (var name in extraNames)
                current.Remove(name);

        if (firstValue is JsonObject existing)
            return existing;

        var created = new JsonObject();
        current[firstKey ?? key] = created;
        return created;
    }

    /// <summary>
    ///     Sets a value by key (case-insensitive).
    /// </summary>
    private static void SetValueIgnoreCase(JsonObject current, string key, string value)
    {
        RemoveIgnoreCase(current, key);
        current[key] = value;
    }

    /// <summary>
    ///     Removes a value by key (case-insensitive).
    /// </summary>
    private static void RemoveIgnoreCase(JsonObject current, string key)
    {
        List<string>? toRemove = null;
        foreach (var kvp in current)
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                (toRemove ??= []).Add(kvp.Key);

        if (toRemove != null)
            foreach (var name in toRemove)
                current.Remove(name);
    }

    /// <summary>
    ///     Validates and ensures the config file exists and is valid JSON.
    /// </summary>
    public static void ValidateConfig(ILogger logger)
    {
        _ = ValidateConfigAndReturnCache(logger);
    }

    /// <summary>
    ///     Validates <c>appsettings.json</c> once and returns the parsed root object for reuse.
    /// </summary>
    internal static JsonObject ValidateConfigAndReturnCache(ILogger logger)
    {
        var configPath = Path.Combine(Shared.RootDirectory, "appsettings.json");

        try
        {
            var configDir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(configDir))
                Directory.CreateDirectory(configDir);

            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, "{}");
                logger.LogInformation("Created default empty config file: {Path}", configPath);
                return new JsonObject();
            }

            var content = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(content))
            {
                File.WriteAllText(configPath, "{}");
                logger.LogWarning("Config file was empty, reset to empty: {Path}", configPath);
                return new JsonObject();
            }

            // Quick check for basic JSON structure before full parse
            var trimmed = content.AsSpan().Trim();
            if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
            {
                File.WriteAllText(configPath, "{}");
                logger.LogWarning(
                    "Config file was not valid JSON, reset to empty: {Path}",
                    configPath
                );
                return new JsonObject();
            }

            if (JsonNode.Parse(content) is not JsonObject parsed)
                throw new JsonException("Config root must be a JSON object.");

            return parsed;
        }
        catch
        {
            File.WriteAllText(configPath, "{}");
            logger.LogWarning("Config file was corrupted, reset to empty: {Path}", configPath);
            return new JsonObject();
        }
    }
}
