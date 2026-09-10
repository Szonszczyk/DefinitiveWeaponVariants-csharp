using DefinitiveWeaponVariants.Interfaces;
using SPTarkov.Server.Core.DI;
using System.Reflection;
using System.Text.Json;

namespace DefinitiveWeaponVariants.Loaders;

/// <summary>
/// Loads the mod configuration before SPT builds its dependency-injection container.
/// </summary>
public class ConfigRegistration : IOnDIConstruct
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        // TemplateItemProperties contains JSON members that differ only by casing.
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task OnDIConstructAsync(IServiceCollection serviceCollection, CancellationToken cancellationToken)
    {
        ConfigData config = await LoadConfigFromDiskAsync(cancellationToken);
        serviceCollection.AddSingleton(config);
    }

    private static async Task<ConfigData> LoadConfigFromDiskAsync(CancellationToken cancellationToken)
    {
        string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Could not determine the mod folder.");
        string configDirectory = Path.Combine(modFolder, "config");
        string configPath = Path.Combine(configDirectory, "config.jsonc");
        string defaultConfigPath = Path.Combine(configDirectory, "defaultConfig.jsonc");

        try
        {
            if (!File.Exists(configPath))
            {
                if (!File.Exists(defaultConfigPath))
                {
                    Console.WriteLine($"[DefinitiveWeaponVariants] Neither config.jsonc nor defaultConfig.jsonc was found in {configDirectory}. Using built-in defaults.");
                    return new ConfigData();
                }

                Console.WriteLine("[DefinitiveWeaponVariants] Config file not found. Copying defaultConfig.jsonc to config.jsonc...");
                File.Copy(defaultConfigPath, configPath);
            }

            ConfigData? config = await DeserializeConfigAsync(configPath, cancellationToken);
            if (config is not null)
                return config;

            Console.WriteLine("[DefinitiveWeaponVariants] Config file is empty. Using built-in defaults.");
            return new ConfigData();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DefinitiveWeaponVariants] Failed to load config: {ex.Message}");

            try
            {
                ConfigData? defaultConfig = await DeserializeConfigAsync(defaultConfigPath, cancellationToken);
                if (defaultConfig is not null)
                {
                    Console.WriteLine("[DefinitiveWeaponVariants] Default config loaded successfully.");
                    return defaultConfig;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception defaultConfigException)
            {
                Console.WriteLine($"[DefinitiveWeaponVariants] Failed to load default config: {defaultConfigException.Message}");
            }

            Console.WriteLine("[DefinitiveWeaponVariants] Using built-in config defaults. Consider reinstalling the mod.");
            return new ConfigData();
        }
    }

    private static async Task<ConfigData?> DeserializeConfigAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ConfigData>(stream, JsonOptions, cancellationToken);
    }
}
