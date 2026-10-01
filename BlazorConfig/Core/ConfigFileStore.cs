using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModConfigEditor.Core;

/// <summary>
/// File I/O is intentionally separate from both Razor and the running mod.
/// Each tab gets its own session; only the short disk transaction is serialized.
/// </summary>
public sealed class ConfigFileStore(string directory, string configFile = "config.jsonc", string defaultFile = "defaultConfig.jsonc")
{
    private readonly string _configPath = Path.GetFullPath(Path.Combine(directory, configFile));
    private readonly string _defaultPath = Path.GetFullPath(Path.Combine(directory, defaultFile));
    private readonly string _schemaPath = Path.GetFullPath(Path.Combine(directory, "configSchema.json"));
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public async Task<EditorSession> LoadAsync()
    {
        var gate = Gates.GetOrAdd(_configPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var schemaBytes = await File.ReadAllBytesAsync(_schemaPath);
            var defaultBytes = await File.ReadAllBytesAsync(_defaultPath);
            var configBytes = File.Exists(_configPath) ? await File.ReadAllBytesAsync(_configPath) : null;
            var schema = JsonSerializer.Deserialize<ConfigSchema>(Decode(schemaBytes), JsonValues.Options)
                ?? throw new InvalidDataException("The schema is empty.");
            schema.Validate();
            var defaults = JsonValues.ParseObject(Decode(defaultBytes));
            var current = configBytes is null ? (JsonObject)defaults.DeepClone() : JsonValues.ParseObject(Decode(configBytes));
            return new EditorSession(schema, new ObjectDraft(schema.RootProperties, current, defaults), defaults,
                Fingerprint(configBytes), Fingerprint(schemaBytes), Fingerprint(defaultBytes));
        }
        finally { gate.Release(); }
    }

    public async Task<SaveResult> SaveAsync(EditorSession session)
    {
        // Validate before creating a temporary file or touching the original.
        var savedConfiguration = session.Draft.Build();
        var output = Encoding.UTF8.GetBytes(savedConfiguration.ToJsonString(JsonValues.Options) + Environment.NewLine);
        var gate = Gates.GetOrAdd(_configPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        string? temporary = null;
        try
        {
            var current = File.Exists(_configPath) ? await File.ReadAllBytesAsync(_configPath) : null;
            if (Fingerprint(current) != session.ConfigFingerprint
                || Fingerprint(await File.ReadAllBytesAsync(_schemaPath)) != session.SchemaFingerprint
                || Fingerprint(await File.ReadAllBytesAsync(_defaultPath)) != session.DefaultFingerprint)
                throw new InvalidDataException("The config, defaults, or schema changed on disk. Reload the page's configuration before saving; your draft has not been written.");

            var folder = Path.GetDirectoryName(_configPath)!;
            var stem = Path.GetFileNameWithoutExtension(_configPath);
            temporary = Path.Combine(folder, $".{stem}.{Guid.NewGuid():N}.tmp");
            // The temporary file is on the same filesystem, so replacement does
            // not expose half-written JSON if the server/process stops mid-save.
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(output);
                stream.Flush(flushToDisk: true);
            }

            if (current is not null)
            {
                // A null backup path replaces the file without creating a backup.
                // Keep the atomic replacement: failed writes must not truncate it.
                File.Replace(temporary, _configPath, destinationBackupFileName: null);
            }
            else File.Move(temporary, _configPath); // No overwrite if another writer created it.
            temporary = null;
            session.ConfigFingerprint = Fingerprint(output);

            return new SaveResult(savedConfiguration);
        }
        finally
        {
            try { if (temporary is not null && File.Exists(temporary)) File.Delete(temporary); }
            finally { gate.Release(); }
        }
    }

    // Accept the UTF-8 BOM used by some Windows editors. Hash original bytes so
    // even a comment-only external edit is detected instead of overwritten.
    private static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
    private static string? Fingerprint(byte[]? bytes) => bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
}

public sealed class EditorSession(ConfigSchema schema, ObjectDraft draft, JsonObject defaults, string? configFingerprint, string? schemaFingerprint, string? defaultFingerprint)
{
    public ConfigSchema Schema { get; } = schema;
    public ObjectDraft Draft { get; } = draft;
    public JsonObject Defaults { get; } = defaults;
    public DraftChangeTracker Changes { get; } = new(draft);
    public string? ConfigFingerprint { get; internal set; } = configFingerprint;
    public string? SchemaFingerprint { get; } = schemaFingerprint;
    public string? DefaultFingerprint { get; } = defaultFingerprint;
}

public sealed record SaveResult(JsonObject SavedConfiguration);
