using System.Diagnostics;
using System.Reflection;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace DefinitiveWeaponVariants.Integrations;

[Injectable(InjectionType.Singleton)]
public class APBSIntegration(
    CustomLogger logger,
    ConfigData config,
    ModHelper modHelper,
    ModDataStorage modDataStorage,
    ModCheck modCheck,
    IHostApplicationLifetime lifetime)
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

    // Normal requests can run concurrently. File updates, rebuilds and recovery
    // have an exclusive lease spanning actual APBS task completion.
    public bool CoordinateRequests => _ready && (config.EnableAPBSWeaponRotation || _store?.Pending == true);

    public void CheckModInstall()
    {
        _installed = modCheck.CheckInstalledMod("com.acidphantasm.progressivebotsystem", config.APBSFolderName);
        if (!_installed && (config.EnableAPBSBlacklistGeneration || config.EnableAPBSWeaponRotation))
            logger.Warning("APBS integration is enabled, but APBS/folder is missing. Normal DWV item import continues.");
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        if (!_installed) return;
        try
        {
            var folder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var parent = Directory.GetParent(folder)?.FullName ?? throw new IOException("DWV parent folder not found.");
            if (string.IsNullOrWhiteSpace(config.APBSFolderName) || Path.GetFileName(config.APBSFolderName) != config.APBSFolderName
                || config.APBSFolderName is "." or "..")
                throw new InvalidDataException("APBSFolderName must be a sibling mod folder name.");
            _blacklistPath = Path.Combine(parent, config.APBSFolderName, "blacklists.json");
            var statePath = Path.Combine(folder, "config", "apbs-weapon-rotation-state.json");
            if (!config.EnableAPBSBlacklistGeneration && !config.EnableAPBSWeaponRotation
                && !File.Exists(statePath)) return;

            ResolveRefreshApi();
            _store = new(statePath, _blacklistPath);
            _store.Load();
            _startupSynchronization = config.EnableAPBSBlacklistGeneration || config.EnableAPBSWeaponRotation || _store.Pending;
            using (await _requests.WriteAsync(token)) await SynchronizeAsync(token);
            _ready = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.Warning($"APBS integration unavailable: {ex.Message} Normal DWV item import continues; files with uncertain ownership are left untouched.");
        }
    }

    private void ResolveRefreshApi()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(ModConfigType, false)).FirstOrDefault(t => t is not null);
        var metadataType = type?.Assembly.GetType("ProgressiveBotSystem.ModMetadata");
        var metadata = metadataType is null ? null : Activator.CreateInstance(metadataType) as IModMetadata;
        _reload = APBSRefreshApi.Resolve(type, config.EnableAPBSWeaponRotation, metadata?.Version.ToString());
    }

    private APBSRotationOptions Options()
    {
        var permanent = APBSRotationPolicy.PermanentExclusions(modDataStorage.AllVariantIds,
            modDataStorage.VariantIdsByQuality, modDataStorage.VariantTypes,
            config.APBSTierConfig, config.APBSBlacklistedVariantTypes);
        return new(config.EnableAPBSBlacklistGeneration, config.EnableAPBSWeaponRotation,
            config.APBSWeaponsAvailable, config.APBSWeaponRotationIntervalMinutes, permanent);
    }

    private Dictionary<int, HashSet<string>> CompatibilityExclusions()
    {
        var tiers = APBSRotationState.EmptyTiers();
        foreach (var (quality, weapons) in modDataStorage.VariantIdsByQuality)
        {
            int minimum = config.APBSTierConfig.GetValueOrDefault(quality);
            foreach (var id in weapons)
            {
                bool typeBanned = modDataStorage.VariantTypes.TryGetValue(id, out var type)
                    && config.APBSBlacklistedVariantTypes.Contains(type);
                for (int tier = 1; tier <= 7; tier++)
                    if (minimum <= 0 || tier < minimum || typeBanned) tiers[tier].Add(id);
            }
        }
        return tiers;
    }

    private bool Due(DateTimeOffset now) => _store is not null && now.UtcTicks >= Interlocked.Read(ref _retryAfterUtcTicks) && (_store.Pending
        || _startupSynchronization || (config.EnableAPBSWeaponRotation && (_store.Journal.Committed.AppliedUtc is null
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
            var options = Options();
            if (_store.Pending)
            {
                await _store.RecoverAsync(RefreshAsync, options.PermanentIds, token);
                refreshed = true;
                logger.Info("APBS interrupted update restored with a confirmed full reload.");
            }
            var plan = APBSRotationPolicy.Build(await File.ReadAllTextAsync(_blacklistPath, token), _store.Journal.Committed,
                modDataStorage.AllVariantIds, CompatibilityExclusions(), options, DateTimeOffset.UtcNow, Random.Shared);
            bool forceRefresh = _startupSynchronization && !refreshed;
            await _store.ApplyAsync(plan, forceRefresh, RefreshAsync, options.PermanentIds, token);
            refreshed |= plan.Changed || forceRefresh;
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
