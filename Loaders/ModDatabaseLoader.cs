using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;
using System.Reflection;

namespace DefinitiveWeaponVariants.Loaders;

[Injectable(InjectionType.Singleton)]
public class ModDatabaseLoader
{
    private readonly string modFolder;
    private readonly CustomLogger _logger;
    private readonly ModHelper _modHelper;
    public Dictionary<string, VariantConfiguration> DbVariants { get; private set; }
    public Dictionary<string, VariantConfiguration> DbItems { get; private set; }
    public Dictionary<string, string> DbShortnames { get; private set; }
    public Dictionary<string, Preset> DbPresets { get; private set; }
    public Dictionary<string, Dictionary<string, string>> DbLocales { get; private set; }
    public ModDatabaseLoader(CustomLogger logger, ModHelper modHelper)
    {
        modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        _logger = logger;
        _modHelper = modHelper;

        DbVariants = LoadDbVariants(Path.Combine(modFolder, "db", "01_Variants"));
        DbItems = LoadDbVariants(Path.Combine(modFolder, "db", "02_Items"));
        DbShortnames = LoadDbShortnames(Path.Combine(modFolder, "db", "03_Shortnames"));
        DbPresets = LoadDbPresets(Path.Combine(modFolder, "db", "04_Presets"));
        DbLocales = LoadDbLocales(Path.Combine(modFolder, "db", "05_Locales"));
    }

    private Dictionary<string, VariantConfiguration> LoadDbVariants(string directoryPath)
    {
        var combinedData = new Dictionary<string, VariantConfiguration>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directoryPath))
        {
            _logger.Warning($"Directory not found: {directoryPath}!");
            return combinedData;
        }

        var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            try
            {
                var data = _modHelper.GetJsonDataFromFile<Dictionary<string, VariantConfiguration>>(modFolder, file);

                if (data == null)
                    continue;

                foreach (var (key, value) in data)
                {
                    if (combinedData.TryGetValue(key, out var existing))
                    {
                        // Merge-only records extend the other record, including replacements.
                        var existingIsMergeOnly = IsMergeOnly(existing);
                        var valueIsMergeOnly = IsMergeOnly(value);
                        if (!existingIsMergeOnly && !valueIsMergeOnly)
                        {
                            if (existing.ReplaceExisting == true && value.ReplaceExisting == true)
                            {
                                _logger.Error($"Duplicate ReplaceExisting conflict for key '{key}' in {Path.GetFileName(file)}. Only one variant config should have 'ReplaceExisting' set to true!");
                                continue;
                            }

                            if (value.ReplaceExisting == true)
                            {
                                combinedData[key] = value;
                                continue;
                            }
                            else if (existing.ReplaceExisting != true)
                            {
                                _logger.Error($"Duplicate variant conflict for key '{key}' in {Path.GetFileName(file)}. A replacement variant config must have 'ReplaceExisting' set to true!");
                            }

                            continue;
                        }

                        var original = valueIsMergeOnly ? existing : value;
                        var duplicate = ReferenceEquals(original, existing) ? value : existing;

                        // --- Merge Weapons ---
                        if (duplicate.Weapons?.Count > 0)
                        {
                            original.Weapons = new HashSet<string>(original.Weapons ?? [], StringComparer.OrdinalIgnoreCase);
                            original.Weapons.UnionWith(duplicate.Weapons);
                        }
                        // --- Merge ModWeapons ---
                        if (duplicate.ModWeapons != null)
                        {
                            original.ModWeapons ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var kv in duplicate.ModWeapons)
                            {
                                // Replace duplicates with the merge-only entry
                                original.ModWeapons[kv.Key] = kv.Value;
                            }
                        }
                        // --- Merge IndividualChanges ---
                        if (duplicate.IndividualChanges != null)
                        {
                            original.IndividualChanges ??= new Dictionary<string, IndividualChangeSet>(StringComparer.OrdinalIgnoreCase);
                            foreach (var kv in duplicate.IndividualChanges)
                            {
                                original.IndividualChanges[kv.Key] = kv.Value;
                            }
                        }
                        // --- Merge Properties ---
                        if (duplicate.Properties != null)
                        {
                            original.Properties ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            foreach (var kv in duplicate.Properties)
                            {
                                original.Properties[kv.Key] = kv.Value;
                            }
                        }
                        combinedData[key] = original;
                    }
                    else
                    {
                        combinedData[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error reading {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return combinedData;
    }

    private static bool IsMergeOnly(VariantConfiguration configuration)
    {
        return string.IsNullOrEmpty(configuration.Description)
            && string.IsNullOrEmpty(configuration.Explanation)
            && string.IsNullOrEmpty(configuration.ShortName)
            && configuration.ItemTplToClone == null
            && configuration.Changes == null
            && configuration.Barter == null
            && configuration.WeaponIdToUseAs == null
            && configuration.HandbookPriceRoubles == null
            && string.IsNullOrEmpty(configuration.VariantType)
            && string.IsNullOrEmpty(configuration.Rarity);
    }

    private Dictionary<string, string> LoadDbShortnames(string directoryPath)
    {
        var combinedData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directoryPath))
        {
            _logger.Warning($"Directory not found: {directoryPath}!");
            return combinedData;
        }

        var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            try
            {
                var data = _modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modFolder, file);

                if (data == null)
                    continue;

                foreach (var kvp in data)
                {
                    combinedData[kvp.Key] = kvp.Value; // overwrite duplicates
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error reading {Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return combinedData;
    }
    private Dictionary<string, Preset> LoadDbPresets(string directoryPath)
    {
        var combinedData = new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(directoryPath))
        {
            _logger.Warning($"Directory not found: {directoryPath}!");
            return combinedData;
        }

        var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            try
            {
                var data = _modHelper.GetJsonDataFromFile<Dictionary<string, Preset>>(modFolder, file);

                if (data == null)
                    continue;

                foreach (var kvp in data)
                {
                    combinedData[kvp.Key] = kvp.Value; // overwrite duplicates
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error reading {Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return combinedData;
    }

    private Dictionary<string, Dictionary<string, string>> LoadDbLocales(string directoryPath)
    {
        var combinedData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directoryPath))
        {
            _logger.Warning($"Directory not found: {directoryPath}!");
            return combinedData;
        }
        var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            try
            {
                var data = _modHelper.GetJsonDataFromFile<Dictionary<string, Dictionary<string, string>>>(modFolder, file);
                if (data == null) continue;
                foreach (var kvp in data)
                {
                    if (combinedData.TryGetValue(kvp.Key, out var existingData))
                    {
                        foreach (var innerKvp in kvp.Value)
                        {
                            existingData[innerKvp.Key] = innerKvp.Value; // overwrite duplicates
                        }
                    }
                    else
                    {
                        combinedData[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error reading {Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return combinedData;
    }
}
