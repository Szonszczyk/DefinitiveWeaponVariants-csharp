using DefinitiveWeaponVariants.Interfaces;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using System.Reflection;

namespace DefinitiveWeaponVariants.Loaders;

[Injectable(InjectionType.Singleton)]
public class ConfigLoader
{
    public ConfigData Config { get; }

    public ConfigLoader(ISptLogger<DefinitiveWeaponVariants> logger, ModHelper modHelper)
    {
        string modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        string configDir = Path.Combine(modFolder, "config");
        string configPath = Path.Combine(configDir, "config.jsonc");
        string defaultConfigPath = Path.Combine(configDir, "defaultConfig.jsonc");

        try
        {
            // Check if config.jsonc exists
            if (!File.Exists(configPath))
            {
                if (File.Exists(defaultConfigPath))
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Config file not found. Copying defaultConfig.jsonc to config.jsonc...", LogTextColor.Yellow);
                    File.Copy(defaultConfigPath, configPath);
                }
                else
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Neither config.jsonc nor defaultConfig.jsonc found in {configDir}. Using built-in defaults. Consider reinstalling the mod!", LogTextColor.Red);
                    Config = new ConfigData();
                    return;
                }
            }

            // Load config.jsonc
            var config = modHelper.GetJsonDataFromFile<ConfigData>(modFolder, configPath);

            if (config == null)
            {
                logger.LogWithColor($"[{GetType().Namespace}] Config file is null. Loading default config.", LogTextColor.Red);
                Config = new ConfigData();
                return;
            }

            Config = config;
            //logger.LogWithColor($"[{GetType().Namespace}] Config loaded successfully.", LogTextColor.Green);
        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[{GetType().Namespace}] Failed to load config: {ex.Message}", LogTextColor.Red, LogBackgroundColor.White);
            try
            {
                var config = modHelper.GetJsonDataFromFile<ConfigData>(modFolder, defaultConfigPath);
                logger.LogWithColor($"[{GetType().Namespace}] Default config loaded successfully", LogTextColor.Yellow);
                Config = config;
            }
            catch (Exception ex2)
            {
                logger.LogWithColor($"[{GetType().Namespace}] Failed to load default config: {ex2.Message}\nThis should not happened. Please don't edit defaultConfig.json! Consider downloading fresh file from Forge!", LogTextColor.Red, LogBackgroundColor.White);
                Config = new ConfigData();
            }
                
        }
    }
}
