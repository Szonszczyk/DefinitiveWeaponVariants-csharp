using DefinitiveWeaponVariants.Models;
using SPTarkov.Server.Core.DI;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace DefinitiveWeaponVariants.Loaders;

/// <summary>Loads configuration before SPT builds its dependency-injection container.</summary>
public class ConfigRegistration : IOnDIConstruct
{
    public const string ConfigFileComment = "// You can edit this config when server is running in your browser at: https://127.0.0.1:6969/definitive-weapon-variants/config";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions DefaultWriteOptions = new(JsonOptions)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { HideInternalTradeSettings }
        }
    };

    public static JsonObject CreateDefaultValues() => JsonSerializer.SerializeToNode(new ConfigData(), DefaultWriteOptions)!.AsObject();

    private static void HideInternalTradeSettings(JsonTypeInfo type)
    {
        if (type.Type != typeof(CustomBarterConfig)) return;
        for (int i = type.Properties.Count - 1; i >= 0; i--)
        {
            var property = type.Properties[i];
            if (property.Name is nameof(CustomBarterConfig.TraderId) or nameof(CustomBarterConfig.RandomAssortWeight))
                type.Properties.RemoveAt(i);
            else if (property.Name == nameof(CustomBarterConfig.BarterPrice))
                property.ShouldSerialize = (_, value) => value is Dictionary<string, int> { Count: > 0 };
            else if (property.Name == nameof(CustomBarterConfig.UnlimitedCount))
                property.ShouldSerialize = (_, value) => value is false;
            else if (property.Name == nameof(CustomBarterConfig.StackObjectsCount))
                property.ShouldSerialize = (_, value) => value is double count && count != 99;
        }
    }

    public static async Task OnDIConstructAsync(IServiceCollection serviceCollection, CancellationToken cancellationToken)
    {
        string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Could not determine the mod folder.");
        var config = await LoadConfigAsync(Path.Combine(modFolder, "config"), cancellationToken);
        serviceCollection.AddSingleton(config);
    }

    private static async Task<ConfigData> LoadConfigAsync(string configDirectory, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(configDirectory, "config.jsonc");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(configPath))
            {
                Directory.CreateDirectory(configDirectory);
                var temporary = Path.Combine(configDirectory, $".config.{Guid.NewGuid():N}.tmp");
                try
                {
                    var text = ConfigFileComment + Environment.NewLine
                        + CreateDefaultValues().ToJsonString(JsonOptions) + Environment.NewLine;
                    await File.WriteAllTextAsync(temporary, text, new UTF8Encoding(false), cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    try { File.Move(temporary, configPath); }
                    catch (IOException) when (File.Exists(configPath)) { /* Another writer created the config first. */ }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                Console.WriteLine("[DefinitiveWeaponVariants] Created config.jsonc from built-in defaults.");
            }

            var config = await DeserializeConfigAsync(configPath, cancellationToken);
            if (config is not null) return config;
            Console.WriteLine("[DefinitiveWeaponVariants] Config is null. Using built-in defaults.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            Console.WriteLine($"[DefinitiveWeaponVariants] Failed to load config: {exception.Message}. Using built-in defaults; the existing file was kept.");
        }
        return new ConfigData();
    }

    private static async Task<ConfigData?> DeserializeConfigAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ConfigData>(stream, JsonOptions, cancellationToken);
    }
}
