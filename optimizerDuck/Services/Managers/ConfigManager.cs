using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Services.Managers;

public class ConfigManager(IConfiguration configuration, ILogger<ConfigManager> logger)
{
    private readonly string _configPath = Path.Combine(Shared.RootDirectory, "appsettings.json");
    private readonly SemaphoreSlim _lock = new(1, 1);
    private JObject _cache = new();

    public async Task InitializeAsync()
    {
        _cache = await LoadConfigAsync();
    }


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

    public void Remove(string key)
    {
        _ = RemoveAsync(key);
    }

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

    private async Task SaveConfigAsync()
    {
        await File.WriteAllTextAsync(
            _configPath,
            _cache.ToString(Formatting.Indented));

        (configuration as IConfigurationRoot)?.Reload();
    }

    private static JToken? GetTokenIgnoreCase(JObject obj, string key)
    {
        var prop = obj.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
        return prop?.Value;
    }

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

    private static void RemoveIgnoreCase(JObject current, string key)
    {
        var toRemove = new List<string>();
        foreach (var p in current.Properties())
            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
                toRemove.Add(p.Name);

        foreach (var name in toRemove) current.Remove(name);
    }

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