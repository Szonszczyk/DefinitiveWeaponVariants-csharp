using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.Server.Core.Models.Common;

namespace DefinitiveWeaponVariants.Integrations;

// Use SPT's MongoId validation. Policy, journal and request coordination can be
// verified without starting the server or loading APBS.
internal sealed class APBSRotationState
{
    public Dictionary<int, HashSet<string>> OwnedPermanent { get; set; } = EmptyTiers();
    public Dictionary<int, HashSet<string>> OwnedCompatibility { get; set; } = EmptyTiers();
    public Dictionary<int, HashSet<string>> OwnedTemporary { get; set; } = EmptyTiers();
    public List<string> Selected { get; set; } = [];
    public Dictionary<string, long> LastSelected { get; set; } = [];
    public long Generation { get; set; }
    public DateTimeOffset? AppliedUtc { get; set; }
    public string PolicyKey { get; set; } = "";

    public static Dictionary<int, HashSet<string>> EmptyTiers() =>
        Enumerable.Range(1, 7).ToDictionary(t => t, _ => new HashSet<string>(StringComparer.Ordinal));
}

internal sealed class APBSRotationJournal
{
    public int Version { get; set; } = 1;
    public string BlacklistPath { get; set; } = "";
    public APBSRotationState Committed { get; set; } = new();
    public APBSPendingUpdate? Pending { get; set; }
}

internal sealed class APBSPendingUpdate
{
    public APBSRotationState Candidate { get; set; } = new();
    public Dictionary<int, HashSet<string>> Added { get; set; } = APBSRotationState.EmptyTiers();
    public Dictionary<int, HashSet<string>> Removed { get; set; } = APBSRotationState.EmptyTiers();
    public string BeforeHash { get; set; } = "";
    public string AfterHash { get; set; } = "";
    public bool Written { get; set; }
}

internal sealed record APBSRotationOptions(bool CompatibilityEnabled, bool RotationEnabled,
    int Allowance, int IntervalMinutes, HashSet<string> PermanentIds);

internal sealed record APBSRotationPlan(string Json, string BeforeHash, APBSRotationState Candidate,
    Dictionary<int, HashSet<string>> Added, Dictionary<int, HashSet<string>> Removed,
    bool Changed, int EligibleCount, int ExcludedCount);

internal static class APBSRotationPolicy
{
    public static HashSet<string> PermanentExclusions(HashSet<string> registry,
        Dictionary<string, HashSet<string>> qualityIds, Dictionary<string, string> variantTypes,
        Dictionary<string, int> tierConfig, IEnumerable<string> blacklistedTypes)
    {
        var types = new HashSet<string>(blacklistedTypes, StringComparer.Ordinal);
        var excluded = variantTypes.Where(entry => types.Contains(entry.Value))
            .Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (quality, ids) in qualityIds)
            if (tierConfig.TryGetValue(quality, out var tier) && tier == 0) excluded.UnionWith(ids);
        excluded.IntersectWith(registry);
        return excluded;
    }

    public static JsonObject Parse(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("APBS blacklist must be a JSON object.");
        if (root["weaponBlacklist"] is not JsonObject weapons)
            throw new InvalidDataException("APBS weaponBlacklist is missing or invalid.");
        for (int tier = 1; tier <= 7; tier++)
        {
            if (weapons[$"tier{tier}Blacklist"] is not JsonArray ids)
                throw new InvalidDataException($"APBS weaponBlacklist/tier{tier}Blacklist must be an array.");
            foreach (var node in ids)
                if (node is not JsonValue value || !value.TryGetValue<string>(out var id) || id is null || !MongoId.IsValidMongoId(id))
                    throw new InvalidDataException($"Invalid weapon ID in APBS tier {tier}; blacklist left untouched.");
        }
        return root;
    }

    public static HashSet<string> Tier(JsonObject root, int tier) =>
        ((JsonArray)root["weaponBlacklist"]![$"tier{tier}Blacklist"]!).Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);

    public static APBSRotationPlan Build(string currentJson, APBSRotationState committed,
        HashSet<string> registry, Dictionary<int, HashSet<string>> compatibility,
        APBSRotationOptions options, DateTimeOffset now, Random random)
    {
        if (options.Allowance <= 0 || options.IntervalMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Allowance and interval must be positive.");
        if (registry.Any(id => !MongoId.IsValidMongoId(id)) || !options.PermanentIds.IsSubsetOf(registry)
            || compatibility.Values.Any(ids => !ids.IsSubsetOf(registry)))
            throw new InvalidDataException("Generated APBS exclusions must contain only registered DWV weapon IDs.");

        var root = Parse(currentJson);
        var baseline = APBSRotationState.EmptyTiers();
        var desiredCompatibility = APBSRotationState.EmptyTiers();
        for (int tier = 1; tier <= 7; tier++)
        {
            baseline[tier] = Tier(root, tier);
            baseline[tier].ExceptWith(committed.OwnedPermanent[tier]);
            baseline[tier].ExceptWith(committed.OwnedCompatibility[tier]);
            baseline[tier].ExceptWith(committed.OwnedTemporary[tier]);
            // Disabling the old generation option retains its existing restrictions.
            desiredCompatibility[tier] = options.CompatibilityEnabled
                ? new(compatibility[tier])
                : committed.OwnedCompatibility[tier].Intersect(Tier(root, tier)).ToHashSet();
        }

        // A partial-tier ban stays in that tier; a weapon eligible in another tier
        // can still take one place in the shared allowance.
        var eligible = registry.Where(id => Enumerable.Range(1, 7).Any(tier =>
            !options.PermanentIds.Contains(id) && !baseline[tier].Contains(id)
            && !desiredCompatibility[tier].Contains(id))).ToHashSet();
        var candidate = APBSRotationStore.Clone(committed);
        candidate.LastSelected = candidate.LastSelected.Where(kv => registry.Contains(kv.Key)).ToDictionary();
        var key = APBSRotationStore.Hash(string.Join("\n", eligible.Order(StringComparer.Ordinal)) + $"\n{options.Allowance}");
        bool due = options.RotationEnabled && (candidate.AppliedUtc is null || candidate.PolicyKey != key
            || now - candidate.AppliedUtc >= TimeSpan.FromMinutes(options.IntervalMinutes));
        if (!options.RotationEnabled)
        {
            candidate.Selected = [];
            candidate.AppliedUtc = null;
            candidate.PolicyKey = "";
        }
        else if (due)
        {
            var shuffled = eligible.Order(StringComparer.Ordinal).ToArray();
            random.Shuffle(shuffled);
            candidate.Selected = shuffled.OrderBy(id => candidate.LastSelected.GetValueOrDefault(id))
                .Take(Math.Min(options.Allowance, eligible.Count)).ToList();
            candidate.Generation = checked(candidate.Generation + 1);
            foreach (var id in candidate.Selected) candidate.LastSelected[id] = candidate.Generation;
            candidate.AppliedUtc = now;
            candidate.PolicyKey = key;
        }

        var selected = candidate.Selected.ToHashSet();
        var temporary = options.RotationEnabled ? eligible.Except(selected).ToHashSet() : [];
        var added = APBSRotationState.EmptyTiers();
        var removed = APBSRotationState.EmptyTiers();
        bool changed = false;
        for (int tier = 1; tier <= 7; tier++)
        {
            var effective = new HashSet<string>(baseline[tier]);
            effective.UnionWith(options.PermanentIds);
            effective.UnionWith(desiredCompatibility[tier]);
            effective.UnionWith(temporary);
            candidate.OwnedPermanent[tier] = options.PermanentIds.Except(baseline[tier]).ToHashSet();
            candidate.OwnedCompatibility[tier] = desiredCompatibility[tier].Except(baseline[tier]).Except(options.PermanentIds).ToHashSet();
            candidate.OwnedTemporary[tier] = temporary.Except(baseline[tier]).Except(desiredCompatibility[tier]).ToHashSet();
            var current = Tier(root, tier);
            added[tier] = effective.Except(current).ToHashSet();
            removed[tier] = current.Except(effective).ToHashSet();
            if (!current.SetEquals(effective))
            {
                changed = true;
                // Preserve existing entry order, append our new entries deterministically.
                var retained = ((JsonArray)root["weaponBlacklist"]![$"tier{tier}Blacklist"]!)
                    .Select(n => n!.GetValue<string>()).Where(effective.Contains).ToList();
                retained.AddRange(added[tier].Order(StringComparer.Ordinal));
                root["weaponBlacklist"]![$"tier{tier}Blacklist"] = new JsonArray(retained.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
            }
        }
        return new(changed ? root.ToJsonString(APBSRotationStore.JsonOptions) + Environment.NewLine : currentJson,
            APBSRotationStore.Hash(currentJson), candidate, added, removed, changed, eligible.Count, temporary.Count);
    }
}

internal sealed class APBSRotationStore(string statePath, string blacklistPath, Func<DateTimeOffset>? completedClock = null)
{
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public APBSRotationJournal Journal { get; private set; } = new() { BlacklistPath = Path.GetFullPath(blacklistPath) };
    public bool Pending => Journal.Pending is not null;
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public void Load()
    {
        if (!File.Exists(statePath)) return;
        var journal = JsonSerializer.Deserialize<APBSRotationJournal>(File.ReadAllText(statePath), JsonOptions)
            ?? throw new InvalidDataException("APBS ownership journal is empty.");
        if (journal.Version != 1 || !string.Equals(journal.BlacklistPath, Path.GetFullPath(blacklistPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("APBS ownership journal has an unsupported version or belongs to another APBS folder.");
        Validate(journal.Committed);
        if (journal.Pending is { } pending)
        {
            Validate(pending.Candidate);
            ValidateTiers(pending.Added);
            ValidateTiers(pending.Removed);
            if (pending.BeforeHash.Length != 64 || pending.AfterHash.Length != 64)
                throw new InvalidDataException("Invalid APBS pending-update fingerprints.");
        }
        Journal = journal;
    }

    private static void Validate(APBSRotationState state)
    {
        ValidateTiers(state.OwnedPermanent);
        ValidateTiers(state.OwnedCompatibility);
        ValidateTiers(state.OwnedTemporary);
        if (state.Selected is null || state.LastSelected is null || state.PolicyKey is null
            || state.Generation < 0 || state.Selected.Any(id => !MongoId.IsValidMongoId(id))
            || state.LastSelected.Any(kv => !MongoId.IsValidMongoId(kv.Key) || kv.Value < 0 || kv.Value > state.Generation))
            throw new InvalidDataException("Invalid APBS ownership/selection state.");
    }

    private static void ValidateTiers(Dictionary<int, HashSet<string>> tiers)
    {
        if (tiers is null || tiers.Count != 7 || Enumerable.Range(1, 7).Any(t => !tiers.TryGetValue(t, out var ids)
            || ids is null || ids.Any(id => id is null || !MongoId.IsValidMongoId(id))))
            throw new InvalidDataException("Invalid APBS tier ownership state.");
    }

    private async Task SaveAsync(APBSRotationJournal journal, CancellationToken token)
    {
        await AtomicWriteAsync(statePath, JsonSerializer.Serialize(journal, JsonOptions) + Environment.NewLine, token: token);
        Journal = journal;
    }

    private Task CommitAsync(APBSRotationPlan plan, CancellationToken token)
    {
        var candidate = Clone(plan.Candidate);
        if (candidate.AppliedUtc is not null && candidate.Generation != Journal.Committed.Generation)
            candidate.AppliedUtc = completedClock?.Invoke() ?? DateTimeOffset.UtcNow;
        return SaveAsync(new() { BlacklistPath = Journal.BlacklistPath, Committed = candidate }, token);
    }

    internal static bool RetryableFileAccess(Exception ex) => OperatingSystem.IsWindows()
        && ex is IOException or UnauthorizedAccessException
        // Access denied, sharing/lock violation, or unable to remove the replaced file.
        // 1176/1177 can indicate a partially completed replacement; never retry those blindly.
        && (ex.HResult & 0xFFFF) is 5 or 32 or 33 or 1175;

    public static async Task AtomicWriteAsync(string path, string contents, string? expectedHash = null,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(contents), token);
                stream.Flush(flushToDisk: true);
            }
            for (int attempt = 0; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    // An editor may change the file while we wait for its handle.
                    // Revalidate on every attempt, not only before the first replace.
                    if (expectedHash is not null && (!File.Exists(path)
                        || Hash(await File.ReadAllTextAsync(path, token)) != expectedHash))
                        throw new IOException("APBS blacklist changed externally; refusing to overwrite it.");
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                    return;
                }
                catch (Exception ex) when (RetryableFileAccess(ex) && attempt < 4)
                {
                    await Task.Delay(100 * (1 << attempt), token);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new IOException($"Atomic write of '{Path.GetFileName(path)}' failed on attempt {attempt + 1}: {ex.Message}", ex);
                }
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task ApplyAsync(APBSRotationPlan plan, bool forceRefresh,
        Func<CancellationToken, Task<string>> refresh, HashSet<string> permanentIds, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Pending) throw new InvalidOperationException("APBS pending recovery must finish before a new update.");
        if (!plan.Changed && !forceRefresh)
        {
            // No runtime mutation needed; the previous successfully applied pool is still valid.
            if (Hash(File.ReadAllText(blacklistPath)) != plan.BeforeHash)
                throw new IOException("APBS blacklist changed externally before policy commit.");
            await CommitAsync(plan, token);
            return;
        }

        var pending = new APBSPendingUpdate
        {
            Candidate = plan.Candidate, Added = plan.Added, Removed = plan.Removed,
            BeforeHash = plan.BeforeHash, AfterHash = Hash(plan.Json)
        };
        await SaveAsync(new() { BlacklistPath = Journal.BlacklistPath, Committed = Journal.Committed, Pending = pending }, token);
        try
        {
            if (plan.Changed) await AtomicWriteAsync(blacklistPath, plan.Json, plan.BeforeHash, token);
            else if (Hash(File.ReadAllText(blacklistPath)) != plan.BeforeHash)
                throw new IOException("APBS blacklist changed externally before refresh.");
            pending.Written = true;
            await SaveAsync(Journal, token);
            token.ThrowIfCancellationRequested();
            var result = await refresh(token);
            if (result != "Success") throw new IOException($"APBS reload returned {result}.");
            // Detect edits while APBS was reading/rebuilding; don't commit a policy
            // whose file no longer matches the applied update.
            if (Hash(File.ReadAllText(blacklistPath)) != pending.AfterHash)
                throw new IOException("APBS blacklist changed externally during refresh.");
            await CommitAsync(plan, token);
        }
        catch (Exception applicationError)
        {
            // Once mutation has begun, recovery uses its own token. Never abandon
            // an APBS task and let requests proceed while it is still mutating pools.
            try { await RecoverAsync(refresh, permanentIds, CancellationToken.None); }
            catch (Exception recoveryError)
            {
                throw new IOException($"{applicationError.Message} Runtime restoration NOT confirmed; recovery journal retained: {recoveryError.Message}", applicationError);
            }
            throw new IOException($"{applicationError.Message} Previous policy restored by a successful APBS reload.", applicationError);
        }
    }

    public async Task RecoverAsync(Func<CancellationToken, Task<string>> refresh, HashSet<string> permanentIds, CancellationToken token)
    {
        if (Journal.Pending is not { } pending) return;
        var restored = Clone(Journal.Committed);
        var json = File.ReadAllText(blacklistPath);
        var hash = Hash(json);
        var root = APBSRotationPolicy.Parse(json);
        if (!pending.Written && hash != pending.BeforeHash && hash != pending.AfterHash)
            throw new IOException("An unconfirmed APBS write and external edits are ambiguous; automatic cleanup refused.");

        // Only reverse positively owned deltas. Preserve fresh external edits and
        // explicitly permanent IDs, including overlapping permanent/temporary bans.
        if (pending.Written || hash == pending.AfterHash)
        {
            for (int tier = 1; tier <= 7; tier++)
            {
                var current = APBSRotationPolicy.Tier(root, tier);
                current.ExceptWith(pending.Added[tier].Except(permanentIds));
                current.UnionWith(pending.Removed[tier]);
                restored.OwnedPermanent[tier].UnionWith(permanentIds.Except(current));
                current.UnionWith(permanentIds);
                // Newly written configured bans survive rollback. Keep ownership
                // so changing the corresponding settings can remove them later.
                restored.OwnedPermanent[tier].UnionWith(pending.Added[tier].Intersect(permanentIds));
                root["weaponBlacklist"]![$"tier{tier}Blacklist"] = new JsonArray(current.Order(StringComparer.Ordinal)
                    .Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
            }
            await AtomicWriteAsync(blacklistPath, root.ToJsonString(JsonOptions) + Environment.NewLine, hash, token);
        }
        var restorationHash = Hash(File.ReadAllText(blacklistPath));
        var result = await refresh(token);
        if (result != "Success") throw new IOException($"APBS restoration reload returned {result}.");
        if (Hash(File.ReadAllText(blacklistPath)) != restorationHash)
            throw new IOException("APBS blacklist changed externally during restoration.");
        await SaveAsync(new() { BlacklistPath = Journal.BlacklistPath, Committed = restored }, token);
    }

    public static async Task<string> ReloadWithRetryAsync(Func<CancellationToken, Task<string>> reload, CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var result = await reload(token);
            if (result != "ActiveProcess" || attempt >= 4) return result;
            await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), token);
        }
    }
}

internal sealed class APBSRequestGate
{
    private readonly SemaphoreSlim _admission = new(1, 1);
    private readonly SemaphoreSlim _empty = new(1, 1);
    private readonly object _sync = new();
    private int _readers;

    public async Task<IDisposable> ReadAsync(CancellationToken token)
    {
        await _admission.WaitAsync(token);
        bool first;
        lock (_sync) { first = _readers == 0; _readers++; }
        try
        {
            if (first)
            {
                try { await _empty.WaitAsync(token); }
                catch { lock (_sync) _readers--; throw; }
            }
            return new Lease(() =>
            {
                lock (_sync) if (--_readers == 0) _empty.Release();
            });
        }
        finally { _admission.Release(); }
    }

    public async Task<IDisposable> WriteAsync(CancellationToken token)
    {
        await _admission.WaitAsync(token);
        try { await _empty.WaitAsync(token); }
        catch { _admission.Release(); throw; }
        return new Lease(() => { _empty.Release(); _admission.Release(); });
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}

internal static class APBSRefreshApi
{
    public static MethodInfo Resolve(Type? type, bool timedRotation, string? version)
    {
        if (type is null) throw new NotSupportedException("APBS ModConfig is not loaded.");
        var method = type.GetMethod("ReloadConfig", BindingFlags.Public | BindingFlags.Static, null, [typeof(CancellationToken)], null);
        if (method is null || !method.ReturnType.IsGenericType || method.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
            throw new NotSupportedException("APBS ReloadConfig(CancellationToken) API is unsupported.");
        var result = method.ReturnType.GenericTypeArguments[0];
        if (!result.IsEnum || !new[] { "Success", "Failure", "ActiveProcess" }.All(Enum.GetNames(result).Contains))
            throw new NotSupportedException("APBS reload result API is unsupported.");
        if (timedRotation && (!Version.TryParse(version?.Split('-')[0], out var parsed) || parsed < new Version(2, 4, 1)
            || Enum.GetNames(result).Contains("InRaid")))
            throw new NotSupportedException("Timed rotation requires APBS >=2.4.1 with an in-raid capable reload API.");
        return method;
    }

    public static async Task<string> InvokeAsync(MethodInfo? method, CancellationToken token)
    {
        if (method is null) return "IntegrationUnavailable";
        try
        {
            if (method.Invoke(null, [token]) is not Task task) return "IntegrationUnavailable";
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task)?.ToString() ?? "Unknown";
        }
        catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
    }
}
