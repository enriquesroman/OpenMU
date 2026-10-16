// <copyright file="DataUpdateService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Service which applies updates of previously initialized data by a <see cref="IDataInitializationPlugIn"/>.
/// </summary>
public class DataUpdateService
{
    private readonly IPersistenceContextProvider _contextProvider;
    private readonly PlugInManager _plugInManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataUpdateService"/> class.
    /// </summary>
    /// <param name="contextProvider">The context provider.</param>
    /// <param name="plugInManager">The plug in manager.</param>
    public DataUpdateService(IPersistenceContextProvider contextProvider, PlugInManager plugInManager)
    {
        this._contextProvider = contextProvider;
        this._plugInManager = plugInManager;
    }

    /// <summary>
    /// Occurs when updates have been installed.
    /// </summary>
    public event AsyncEventHandler? UpdatesInstalled;

    /// <summary>
    /// Determines the available updates which are not installed yet,
    /// or which are installed in a previous version.
    /// </summary>
    /// <returns>The available plugins.</returns>
    /// <exception cref="System.InvalidOperationException">The plugin manager is not initialized.</exception>
    public async ValueTask<IReadOnlyCollection<IConfigurationUpdatePlugIn>> DetermineAvailableUpdatesAsync()
    {
        using var context = this._contextProvider.CreateNewContext();
        var installedVersions = await GetInstalledVersionsAsync(context).ConfigureAwait(false);

        var initializationKey = await DetermineInitializationKeyAsync(context).ConfigureAwait(false);

        var updateStrategyProvider = this._plugInManager.GetStrategyProvider<Guid, IConfigurationUpdatePlugIn>();
        if (updateStrategyProvider is null)
        {
            // it's null when there are no plugins yet ...
            return [];
        }

        var pending = updateStrategyProvider.AvailableStrategies
            .Where(up => up.DataInitializationKey == initializationKey)
            .Where(up => !installedVersions.TryGetValue(up.Key, out var installedVersion) || installedVersion < up.Version)
            .ToList();

        return OrderByDependencies(pending, installedVersions);
    }

    /// <summary>
    /// Gets the installed versions and installation dates of all configuration updates, by update key.
    /// </summary>
    /// <returns>A dictionary of the installed versions and installation dates by update key.</returns>
    public async ValueTask<IReadOnlyDictionary<Guid, (int Version, DateTime? InstalledAt)>> GetInstalledUpdatesAsync()
    {
        using var context = this._contextProvider.CreateNewContext();
        return (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
            .Where(up => up.InstalledAt is not null)
            .GroupBy(up => up.Key)
            .ToDictionary(group => group.Key, group => (group.Max(up => up.Version), group.Max(up => up.InstalledAt)));
    }

    /// <summary>
    /// Applies the updates asynchronous.
    /// </summary>
    /// <param name="updates">The updates. They are validated and ordered by their dependencies before anything is applied, so callers may pass a partial or unordered selection.</param>
    /// <param name="progress">The progress provider. Reports the progress back to the caller.</param>
    public async ValueTask ApplyUpdatesAsync(IReadOnlyList<IConfigurationUpdatePlugIn> updates, IProgress<(Guid CurrentUpdatingKey, bool IsCompleted)> progress)
    {
        using var context = this._contextProvider.CreateNewContext();
        var installedVersions = await GetInstalledVersionsAsync(context).ConfigureAwait(false);

        var orderedUpdates = OrderByDependencies(updates, installedVersions);
        var exceptions = new List<Exception>();

        foreach (var update in orderedUpdates)
        {
            progress.Report((update.Key, false));
            try
            {
                using var updateContext = this._contextProvider.CreateNewContext();
                var gameConfiguration = (await updateContext.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
                var updateStates = await updateContext.GetAsync<ConfigurationUpdateState>().ConfigureAwait(false);
                var updateState = updateStates.FirstOrDefault() ?? updateContext.CreateNew<ConfigurationUpdateState>();

                await update.ApplyUpdateAsync(updateContext, gameConfiguration).ConfigureAwait(false);
                updateState.InitializationKey = update.DataInitializationKey;
                await updateContext.SaveChangesAsync().ConfigureAwait(false);
                await this.SynchronizeCachedMerchantStoresAsync(gameConfiguration).ConfigureAwait(false);
                progress.Report((update.Key, true));
            }
            catch (Exception ex)
            {
                exceptions.Add(new InvalidOperationException($"Error applying update {update.Name} ({update.Key}): {ex.Message}", ex));
            }
        }

        progress.Report((Guid.Empty, true));
        this.UpdatesInstalled?.SafeInvokeAsync();

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }

    /// <summary>
    /// Orders the pending updates by their dependencies. Independent updates are ordered by <see cref="IConfigurationUpdatePlugIn.UpdatedAt"/>.
    /// </summary>
    /// <remarks>
    /// A version bump is ordered by its own <see cref="IConfigurationUpdatePlugIn.UpdatedAt"/>,
    /// so it can run after a newer independent update it used to run before.
    /// That is harmless as long as real dependencies are declared with
    /// <see cref="IConfigurationUpdatePlugIn.DependsOn"/>.
    /// </remarks>
    /// <param name="pending">The pending updates.</param>
    /// <param name="installedVersions">The installed versions by update key.</param>
    /// <returns>The ordered updates.</returns>
    internal static IReadOnlyList<IConfigurationUpdatePlugIn> OrderByDependencies(
        IReadOnlyCollection<IConfigurationUpdatePlugIn> pending,
        IReadOnlyDictionary<Guid, int> installedVersions)
    {
        var pendingByKey = pending.ToDictionary(p => p.Key);

        foreach (var plugin in pending)
        {
            var missing = plugin.DependsOn
                .Where(dep => !IsDependencySatisfied(dep, installedVersions, pendingByKey))
                .Select(dep => dep.Key)
                .Distinct()
                .ToList();
            if (missing.Count > 0)
            {
                throw new MissingUpdateDependencyException(plugin, missing);
            }
        }

        var result = new List<IConfigurationUpdatePlugIn>();
        var visited = new HashSet<Guid>();
        var visiting = new HashSet<Guid>();

        void Visit(IConfigurationUpdatePlugIn plugin)
        {
            if (visited.Contains(plugin.Key))
            {
                return;
            }

            if (!visiting.Add(plugin.Key))
            {
                throw new CircularUpdateDependencyException(plugin);
            }

            foreach (var depKey in plugin.DependsOn
                .Select(dependency => dependency.Key)
                .Where(pendingByKey.ContainsKey))
            {
                Visit(pendingByKey[depKey]);
            }

            visiting.Remove(plugin.Key);
            visited.Add(plugin.Key);
            result.Add(plugin);
        }

        foreach (var plugin in pending.OrderBy(p => p.UpdatedAt).ThenBy(p => p.Key))
        {
            Visit(plugin);
        }

        return result;
    }

    /// <summary>
    /// Determines the key of the data initialization which created the configuration.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>The key of the <see cref="IDataInitializationPlugIn"/>.</returns>
    internal static async ValueTask<string> DetermineInitializationKeyAsync(IContext context)
    {
        var updateStates = await context.GetAsync<ConfigurationUpdateState>().ConfigureAwait(false);
        if (updateStates.FirstOrDefault() is { InitializationKey: not null } updateState)
        {
            return updateState.InitializationKey;
        }

        // Now it's getting tricky ...
        var clientDefinitions = await context.GetAsync<GameClientDefinition>().ConfigureAwait(false);
        if (clientDefinitions.FirstOrDefault() is not { } clientDefinition)
        {
            throw new InvalidOperationException("No data installed");
        }

        return (clientDefinition.Season, clientDefinition.Episode) switch
        {
            (6, 3) => VersionSeasonSix.DataInitialization.Id,
            (0, 75) => Version075.DataInitialization.Id,
            (0, 95) => Version095d.DataInitialization.Id,
            _ => throw new InvalidOperationException($"Unknown client version: {clientDefinition}."),
        };
    }

    private static bool IsDependencySatisfied(
        UpdateDependency dependency,
        IReadOnlyDictionary<Guid, int> installedVersions,
        IReadOnlyDictionary<Guid, IConfigurationUpdatePlugIn> pendingByKey)
    {
        return (installedVersions.TryGetValue(dependency.Key, out var installedVersion)
                && (dependency.MinVersion is null || installedVersion >= dependency.MinVersion))
            || (pendingByKey.TryGetValue(dependency.Key, out var pending)
                && (dependency.MinVersion is null || pending.Version >= dependency.MinVersion));
    }

    private static async ValueTask<IReadOnlyDictionary<Guid, int>> GetInstalledVersionsAsync(IContext context)
    {
        return (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
            .Where(up => up.InstalledAt is not null)
            .GroupBy(up => up.Key)
            .ToDictionary(group => group.Key, group => group.Max(up => up.Version));
    }

    private async ValueTask SynchronizeCachedMerchantStoresAsync(GameConfiguration updatedConfiguration)
    {
        using var configContext = this._contextProvider.CreateNewConfigurationContext();
        if ((await configContext.GetAsync<GameConfiguration>().ConfigureAwait(false)).FirstOrDefault() is not { } cachedConfig
            || ReferenceEquals(cachedConfig, updatedConfiguration))
        {
            return;
        }

        foreach (var monster in updatedConfiguration.Monsters)
        {
            if (monster.MerchantStore is not { } updatedStore
                || cachedConfig.Monsters.FirstOrDefault(m => m.Number == monster.Number)?.MerchantStore is not { } cachedStore)
            {
                continue;
            }

            (cachedStore as IAssignable<ItemStorage>)?.AssignValuesOf(updatedStore, cachedConfig);
            foreach (var item in updatedStore.Items)
            {
                if (cachedStore.Items.FirstOrDefault(i => i.Equals(item)) is IAssignable<Item> cachedItem)
                {
                    cachedItem.AssignValuesOf(item, cachedConfig);
                }
            }
        }
    }
}
