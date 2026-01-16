using DefinitiveWeaponVariants.Interfaces;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using System.Reflection;

namespace DefinitiveWeaponVariants.Loaders
{
    
    internal class ModDatabaseLoader
    {
        private readonly string modFolder;
        private readonly ISptLogger<DefinitiveWeaponVariants> _logger;
        private readonly ModHelper _modHelper;
        public Dictionary<string, VariantConfiguration> DbVariants { get; private set; }
        public Dictionary<string, VariantConfiguration> DbItems { get; private set; }
        public Dictionary<string, string> DbShortnames { get; private set; }
        public Dictionary<string, Preset> DbPresets { get; private set; }
        public ModDatabaseLoader(ISptLogger<DefinitiveWeaponVariants> logger, ModHelper modHelper) 
        {
            modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            _logger = logger;
            _modHelper = modHelper;

            DbVariants = LoadDbVariants(Path.Combine(modFolder, "db", "01_Variants"));
            DbItems = LoadDbVariants(Path.Combine(modFolder, "db", "02_Items"));
            DbShortnames = LoadDbShortnames(Path.Combine(modFolder, "db", "03_Shortnames"));
            DbPresets = LoadDbPresets(Path.Combine(modFolder, "db", "04_Presets"));
        }

        private Dictionary<string, VariantConfiguration> LoadDbVariants(string directoryPath)
        {
            var combinedData = new Dictionary<string, VariantConfiguration>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWithColor($"[{GetType().Namespace}] Directory not found: {directoryPath}!", LogTextColor.Yellow);
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
                            // Both have Description → log error and skip
                            if (!string.IsNullOrEmpty(existing.Description) && !string.IsNullOrEmpty(value.Description))
                            {
                                _logger.LogWithColor($"[{GetType().Namespace}] Duplicate Description conflict for key '{key}' in {Path.GetFileName(file)}. Only one variant config should have 'Description' property!", LogTextColor.Red);
                                continue;
                            }

                            // Determine which is the "original" (the one with Description)
                            var original = !string.IsNullOrEmpty(existing.Description) ? existing : value;
                            var duplicate = ReferenceEquals(original, existing) ? value : existing;

                            // --- Merge Weapons ---
                            if (duplicate.Weapons?.Count > 0)
                            {
                                original.Weapons ??= new List<string>();

                                original.Weapons = original.Weapons
                                    .Union(duplicate.Weapons, StringComparer.OrdinalIgnoreCase)
                                    .ToList();
                            }

                            // --- Merge IndividualChanges ---
                            if (duplicate.IndividualChanges != null)
                            {
                                original.IndividualChanges ??= new Dictionary<string, IndividualChangeSet>(StringComparer.OrdinalIgnoreCase);

                                foreach (var kv in duplicate.IndividualChanges)
                                {
                                    // Replace duplicates with newer entry
                                    original.IndividualChanges[kv.Key] = kv.Value;
                                }
                            }

                            // --- Merge Properties ---
                            if (duplicate.Properties != null)
                            {
                                original.Properties ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                                foreach (var kv in duplicate.Properties)
                                {
                                    // Replace duplicates with newer entry
                                    original.Properties[kv.Key] = kv.Value;
                                }
                            }

                            // Replace combined version back into main dictionary
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
                    _logger.LogWithColor($"[{GetType().Namespace}] Error reading {Path.GetFileName(file)}: {ex.Message}", LogTextColor.Red);
                }
            }

            return combinedData;
        }
        private Dictionary<string, string> LoadDbShortnames(string directoryPath)
        {
            var combinedData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWithColor($"[{GetType().Namespace}] Directory not found: {directoryPath}!", LogTextColor.Yellow);
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
                    _logger.LogWithColor($"[{GetType().Namespace}] Error reading {Path.GetFileName(file)}: {ex.Message}", LogTextColor.Red);
                }
            }
            return combinedData;
        }
        private Dictionary<string, Preset> LoadDbPresets(string directoryPath)
        {
            var combinedData = new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWithColor($"[{GetType().Namespace}] Directory not found: {directoryPath}!", LogTextColor.Yellow);
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
                    _logger.LogWithColor($"[{GetType().Namespace}] Error reading {Path.GetFileName(file)}: {ex.Message}", LogTextColor.Red);
                }
            }
            return combinedData;
        }
    }
}
