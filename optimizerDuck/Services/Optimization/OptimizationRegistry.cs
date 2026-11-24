using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Services.History;
using optimizerDuck.Services.Revert;
using optimizerDuck.Services.System;

namespace optimizerDuck.Services.Optimization;

public class OptimizationRegistry(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<OptimizationRegistry>();
    private HashSet<Guid> _activeIds = [];

    /// <summary>Gets or sets the optimization categories discovered by preloading.</summary>
    public IOptimizationCategory[] OptimizationCategories { get; set; } = [];

    /// <summary>Gets revert payloads on disk that belong to retired optimization IDs.</summary>
    public IReadOnlyList<LegacyRevertItem> LegacyRevertItems { get; private set; } = [];

    /// <summary>Raised whenever <see cref="LegacyRevertItems"/> is refreshed or pruned.</summary>
    public event Action? LegacyRevertsChanged;

    /// <summary>
    ///     Gets a value that indicates whether the optimizations have been fully discovered and
    ///     their applied states loaded.
    /// </summary>
    public bool IsPreloaded { get; private set; }

    private readonly Lock _preloadLock = new();
    private Task? _preloadTask;

    /// <summary>
    ///     Ensures categories and applied-state are loaded before the optimize UI binds.
    ///     Concurrent callers share a single discovery; a failed discovery is retried.
    /// </summary>
    public Task EnsurePreloadedAsync()
    {
        if (IsPreloaded)
            return Task.CompletedTask;

        Task task;
        lock (_preloadLock)
        {
            if (IsPreloaded)
                return Task.CompletedTask;
            _preloadTask ??= PreloadOptimizationsAsync();
            task = _preloadTask;
        }

        return AwaitPreloadAsync(task);

        async Task AwaitPreloadAsync(Task t)
        {
            try
            {
                await t.ConfigureAwait(false);
            }
            finally
            {
                if (!IsPreloaded)
                {
                    lock (_preloadLock)
                    {
                        if (!IsPreloaded && ReferenceEquals(_preloadTask, t))
                            _preloadTask = null;
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Discovers the optimization categories by reflection and loads the applied state of
    ///     their optimizations from revert data on disk.
    /// </summary>
    public async Task PreloadOptimizationsAsync()
    {
        var seenIds = new HashSet<Guid>();
        // Run reflection work on background thread to avoid blocking startup
        var optimizationCategories = await Task.Run(() =>
                CategoryDiscovery.Discover<IOptimizationCategory, IOptimization>(
                    nameof(IOptimizationCategory.Optimizations),
                    t =>
                        CategoryDiscovery.NestedItems<IOptimization>(
                            t,
                            (opt, owner) =>
                            {
                                if (opt is BaseOptimization bo)
                                {
                                    bo.OwnerType = owner;
                                    OptimizationValidation.Validate(bo, seenIds);
                                    ConditionValidation.Validate(
                                        bo.ConditionType,
                                        bo.OptimizationKey
                                    );
                                }
                            }
                        ),
                    c => (int)c.Order
                )
            )
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Total {CategoryCount} categories and {OptimizationCount} optimizations found",
            optimizationCategories.Length,
            optimizationCategories.Sum(c => c.Optimizations.Count)
        );

        _activeIds = seenIds;
        await OptimizationService
            .UpdateOptimizationStateAsync(
                SystemInfo.Unknown,
                optimizationCategories.SelectMany(c => c.Optimizations)
            )
            .ConfigureAwait(false);

        await RefreshLegacyRevertsAsync().ConfigureAwait(false);

        OptimizationCategories = optimizationCategories;
        IsPreloaded = true;
    }

    /// <summary>Scans the revert directory for files belonging to retired optimization IDs.</summary>
    public async Task RefreshLegacyRevertsAsync()
    {
        LegacyRevertItems = await ScanLegacyRevertsAsync(_activeIds, _logger);
        LegacyRevertsChanged?.Invoke();
    }

    /// <summary>Removes a reverted legacy item from the cached list.</summary>
    public void RemoveLegacyRevertItem(Guid id)
    {
        LegacyRevertItems = [.. LegacyRevertItems.Where(item => item.Id != id)];
        LegacyRevertsChanged?.Invoke();
    }

    internal static async Task<IReadOnlyList<LegacyRevertItem>> ScanLegacyRevertsAsync(
        IReadOnlySet<Guid> activeIds,
        ILogger? logger = null
    )
    {
        if (!Directory.Exists(Shared.RevertDirectory))
            return [];

        var items = new List<LegacyRevertItem>();
        foreach (var file in Directory.EnumerateFiles(Shared.RevertDirectory, "*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!Guid.TryParse(fileName, out var id) || activeIds.Contains(id))
                continue;

            try
            {
                var data = await RevertManager.GetRevertDataAsync(id).ConfigureAwait(false);
                if (data is null)
                    continue;

                var stepCount = data.Steps.Count(static s => s is not null);
                if (stepCount == 0)
                    continue;

                var record = ChangeRecordStore.TryRead(id, logger);
                var key = string.IsNullOrWhiteSpace(data.OptimizationName)
                    ? id.ToString()
                    : data.OptimizationName;
                var displayName = !string.IsNullOrWhiteSpace(record?.LogName)
                    ? record.LogName
                    : key;

                items.Add(new LegacyRevertItem(id, key, displayName, data.AppliedAt, stepCount));
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to inspect legacy revert file {Path}", file);
            }
        }

        return [.. items.OrderByDescending(i => i.AppliedAt)];
    }

    /// <summary>
    ///     Gets the category with the given runtime type, which must have been preloaded.
    /// </summary>
    /// <param name="type">The runtime type of the category to retrieve.</param>
    /// <returns>The matching <see cref="IOptimizationCategory"/> instance.</returns>
    public IOptimizationCategory GetCategory(Type type)
    {
        return OptimizationCategories.First(c => c.GetType() == type);
    }
}
