using System.Diagnostics;
using System.Reflection;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Models;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Common;

namespace DefinitiveWeaponVariants.Integrations;

[Injectable(InjectionType.Singleton)]
public class APBSIntegration(
    CustomLogger logger,
    ConfigData config,
    ModHelper modHelper,
    ModDataStorage modDataStorage,
    ModCheck modCheck,
    IHostApplicationLifetime lifetime,
    ModDatabaseLoader modDatabaseLoader,
    IdDatabaseManager idDatabaseManager)
{
    private const string ModConfigType = "ProgressiveBotSystem.Globals.ModConfig";
    private readonly APBSRequestGate _requests = new();
    private APBSRotationStore? _store;
    private string? _blacklistPath;
    private MethodInfo? _reload;
    private bool _installed;
    private volatile bool _ready;
    private long _retryAfterUtcTicks;
    private volatile bool _startupSynchronization;
    private volatile bool _cleanupPending;
    private volatile bool _initializationCompleted;
    private Exception? _initializationError;
    private int _externalUpdates;

    private bool RotationEnabled => config.EnableAPBSWeaponRotation && !config.CleanupAPBSBlacklist;

    // Normal requests can run concurrently. File updates, rebuilds and recovery
    // have an exclusive lease spanning actual APBS task completion.
    public bool CoordinateRequests => (_ready && (RotationEnabled || _startupSynchronization || _cleanupPending || _store?.Pending == true))
        || Volatile.Read(ref _externalUpdates) > 0;

    public void CheckModInstall()
    {
        _installed = modCheck.CheckInstalledMod("com.acidphantasm.progressivebotsystem", config.APBSFolderName);
        if (!_installed && (config.EnableAPBSBlacklistGeneration || config.EnableAPBSWeaponRotation || config.CleanupAPBSBlacklist))
            logger.Warning("APBS integration is enabled, but APBS/folder is missing. Normal DWV item import continues.");
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        AmonyaIntegration.Initialize(this);
        try
        {
            if (!_installed) return;
            var folder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var parent = Directory.GetParent(folder)?.FullName ?? throw new IOException("DWV parent folder not found.");
            if (string.IsNullOrWhiteSpace(config.APBSFolderName) || Path.GetFileName(config.APBSFolderName) != config.APBSFolderName
                || config.APBSFolderName is "." or "..")
                throw new InvalidDataException("APBSFolderName must be a sibling mod folder name.");
            _blacklistPath = Path.Combine(parent, config.APBSFolderName, "blacklists.json");
            var statePath = Path.Combine(folder, "config", "apbs-weapon-rotation-state.json");
            bool generationEnabled = config.EnableAPBSBlacklistGeneration || config.EnableAPBSWeaponRotation;
            _cleanupPending = config.CleanupAPBSBlacklist || (!generationEnabled && File.Exists(statePath));
            if (!generationEnabled && !_cleanupPending) return;

            ResolveRefreshApi();
            _store = new(statePath, _blacklistPath);
            _store.Load();
            _startupSynchronization = true;
            _ready = true;
            if (RotationEnabled && !_cleanupPending)
                logger.Info("APBS rotation loaded. First synchronization is deferred until a raid-start or bot-generation request; APBS's existing startup pool remains in use until then.");
            else
                using (await _requests.WriteAsync(token)) await SynchronizeAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _initializationError = ex;
            logger.Warning($"APBS integration unavailable: {ex.Message} Normal DWV item import continues; files with uncertain ownership are left untouched.");
        }
        finally { _initializationCompleted = true; }
    }

    internal async Task RunExternalBlacklistUpdateAsync(string blacklistPath,
        Func<CancellationToken, Task> update, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!_initializationCompleted || !_installed || _blacklistPath is null)
            throw new InvalidOperationException("DWV APBS coordination has not initialized. Check DWV's APBSFolderName setting.");
        if (_initializationError is not null)
            throw new InvalidOperationException("DWV APBS initialization failed; external blacklist updates are deferred.", _initializationError);
        if (!string.Equals(Path.GetFullPath(blacklistPath), Path.GetFullPath(_blacklistPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Amonya and DWV resolved different APBS blacklist files. Check DWV's APBSFolderName setting.");

        // Also activate request coordination when DWV's own generation modes are off.
        Interlocked.Increment(ref _externalUpdates);
        try
        {
            using (await _requests.WriteAsync(token))
            {
                if (_store?.Pending == true)
                {
                    var permanent = _cleanupPending ? new HashSet<string>() : Options().PermanentIds;
                    await _store.RecoverAsync(RefreshAsync, permanent, token);
                }
                // The caller owns its transaction, including refresh and rollback.
                // Do not synchronize or rotate DWV weapons just to update ammo.
                await update(token);
            }
        }
        finally { Interlocked.Decrement(ref _externalUpdates); }
    }

    private void ResolveRefreshApi()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(ModConfigType, false)).FirstOrDefault(t => t is not null);
        var metadataType = type?.Assembly.GetType("ProgressiveBotSystem.ModMetadata");
        var metadata = metadataType is null ? null : Activator.CreateInstance(metadataType) as IModMetadata;
        _reload = APBSRefreshApi.Resolve(type, RotationEnabled, metadata?.Version.ToString());
    }

    private APBSRotationOptions Options()
    {
        var permanent = APBSRotationPolicy.PermanentExclusions(
            modDataStorage.WeaponVariantsInfo,
            config.APBSTierConfig,
            config.APBSBlacklistedVariantTypes
        );
        return new(config.EnableAPBSBlacklistGeneration || RotationEnabled, RotationEnabled,
            config.APBSWeaponsAvailable, config.APBSWeaponRotationIntervalMinutes, permanent);
    }

    private HashSet<string> CleanupRegistry()
    {
        var ids = new HashSet<string>(modDataStorage.WeaponVariantsInfo.Keys.ToHashSet(), StringComparer.Ordinal);
        // Include saved IDs for variants disabled in the current generation config.
        // Look up existing IDs only; cleanup must never create new IDs or remove cores/items.
        foreach (var variant in modDatabaseLoader.DbVariants.Values)
        foreach (var weapon in variant.Weapons.Concat(variant.ModWeapons.Keys))
        {
            var name = modDatabaseLoader.DbShortnames.ContainsKey(weapon) ? weapon
                : modDatabaseLoader.DbShortnames.FirstOrDefault(entry => entry.Value == weapon).Key ?? weapon;
            if (idDatabaseManager.DbIds.TryGetValue($"{name}{variant.ShortName}:ID", out var id) && MongoId.IsValidMongoId(id))
                ids.Add(id);
        }
        return ids;
    }

    private Dictionary<int, HashSet<string>> CompatibilityExclusions()
    {
        var tiers = APBSRotationState.EmptyTiers();
        foreach (var (quality, weapons) in modDataStorage.WeaponVariantsInfo.GetWeaponsByQuality())
        {
            int minimum = config.APBSTierConfig.GetValueOrDefault(quality);
            foreach (var id in weapons)
            {
                bool typeBanned = modDataStorage.WeaponVariantsInfo.GetVariantTypes().TryGetValue(id, out var type)
                    && config.APBSBlacklistedVariantTypes.Contains(type);
                for (int tier = 1; tier <= 7; tier++)
                    if (minimum <= 0 || tier < minimum || typeBanned) tiers[tier].Add(id);
            }
        }
        return tiers;
    }

    private bool Due(DateTimeOffset now) => _store is not null && now.UtcTicks >= Interlocked.Read(ref _retryAfterUtcTicks) && (_store.Pending
        || _startupSynchronization || _cleanupPending || (RotationEnabled && (_store.Journal.Committed.AppliedUtc is null
        || now - _store.Journal.Committed.AppliedUtc >= TimeSpan.FromMinutes(config.APBSWeaponRotationIntervalMinutes))));

    public async Task HandleRequestAsync(Func<Task> handleNormally, bool rotationTrigger, CancellationToken requestToken)
    {
        if (!CoordinateRequests) { await handleNormally(); return; }
        if (rotationTrigger && Due(DateTimeOffset.UtcNow))
        {
            // A disconnected requester must not cancel a rebuild for everybody.
            // Honor cancellation while queueing, then use server shutdown instead.
            using (await _requests.WriteAsync(requestToken))
            {
                if (Due(DateTimeOffset.UtcNow)) await SynchronizeAsync(lifetime.ApplicationStopping);
            }
        }
        using (await _requests.ReadAsync(requestToken)) await handleNormally();
    }

    private async Task SynchronizeAsync(CancellationToken token)
    {
        if (_store is null || _blacklistPath is null) return;
        var stopwatch = Stopwatch.StartNew();
        bool refreshed = false;
        try
        {
            if (_cleanupPending)
            {
                int removed = await _store.CleanupAsync(CleanupRegistry(), RefreshAsync, token);
                _cleanupPending = false;
                _startupSynchronization = false;
                Interlocked.Exchange(ref _retryAfterUtcTicks, 0);
                if (config.CleanupAPBSBlacklist && removed == 0)
                    logger.Warning("APBS blacklist cleanup removed no DWV weapon entries. Disable CleanupAPBSBlacklist in DWV's configuration and restart the server; the mod cannot turn this option off automatically.");
                else
                    logger.Info($"APBS blacklist cleanup removed {removed} DWV weapon tier entries and deleted its rotation state."
                        + (config.CleanupAPBSBlacklist ? " Disable CleanupAPBSBlacklist and restart to resume generation/rotation." : " Both generation modes are disabled."));
                return;
            }
            var options = Options();
            if (_store.Pending)
            {
                await _store.RecoverAsync(RefreshAsync, options.PermanentIds, token);
                refreshed = true;
                logger.Info("APBS interrupted update restored with a confirmed full reload.");
            }
            var plan = APBSRotationPolicy.Build(await File.ReadAllTextAsync(_blacklistPath, token), _store.Journal.Committed,
                modDataStorage.WeaponVariantsInfo.Keys.ToHashSet(), CompatibilityExclusions(), options, DateTimeOffset.UtcNow, Random.Shared);
            // APBS has already imported items and applied the persisted blacklist
            // during its normal startup. Rebuild only when our policy changes it.
            await _store.ApplyAsync(plan, RefreshAsync, options.PermanentIds, token);
            refreshed |= plan.Changed;
            _startupSynchronization = false;
            Interlocked.Exchange(ref _retryAfterUtcTicks, 0);
            logger.Info($"APBS weapons: selected {plan.Candidate.Selected.Count}/{plan.EligibleCount}, rotation-excluded {plan.ExcludedCount}; full refresh {(refreshed ? "yes" : "no")}; {stopwatch.ElapsedMilliseconds} ms.");
            logger.Debug($"APBS selected IDs: {string.Join(", ", plan.Candidate.Selected)}");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _retryAfterUtcTicks, DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks);
            logger.Warning($"APBS synchronization failed after {stopwatch.ElapsedMilliseconds} ms: {ex.Message} Rotation not committed; retry on a relevant request after 60 seconds. Normal request handling continues.");
        }
    }

    private Task<string> RefreshAsync(CancellationToken token) =>
        APBSRotationStore.ReloadWithRetryAsync(ReloadIfInstalledAsync, token);

    // Await actual completion: never release the gate while a timed-out APBS
    // task could still be mutating shared data.
    private Task<string> ReloadIfInstalledAsync(CancellationToken cancellationToken) => APBSRefreshApi.InvokeAsync(_reload, cancellationToken);
}
