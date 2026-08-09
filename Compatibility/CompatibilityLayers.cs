using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Utils;
using System.Reflection;
using Path = System.IO.Path;

namespace DefinitiveWeaponVariants.Compatibility;

[Injectable(InjectionType.Singleton)]
public class CompatibilityLayers(
    CustomLogger logger,
    ConfigData config,
    ModHelper modHelper,
    JsonUtil jsonUtil,
    IReadOnlyList<SptMod> modlist,
    ModDataStorage modDataStorage
)
{
    private readonly ConfigData modConfig = config;

    public void CheckMods()
    {
        if (modConfig.AmonyaTraderMode)
        {
            if (!ModCheck("com.szonszczyk.amonya"))
            {
                logger.Warning($"Config option \"AmonyaTraderMode\" was enabled but Amonya mod is missing");
                modConfig.AmonyaTraderMode = false;
            }
            else
            {
                modConfig.SpecialAmmoBuyableEnabled = false;
            }
        }
        if (modConfig.EnableAPBSBlacklistGeneration && !ModCheck("com.acidphantasm.progressivebotsystem", modConfig.APBSFolderName))
        {
            logger.Warning($"Config option \"EnableAPBSBlacklistGeneration\" was enabled but APBS mod is missing (or APBS folder was not found)");
            modConfig.EnableAPBSBlacklistGeneration = false;
        }
    }

    public void RunCompatibilityLayers()
    {
        if (modConfig.EnableAPBSBlacklistGeneration) RunCompatibilityLayerAPBS();
    }

    private bool ModCheck(string guid, string? modfolder = null)
    {
        var mod = modlist.ToList().Find(t => t.ModMetadata.ModGuid == guid);
        if (mod is null) return false;
        if (modfolder == null) return true;
        var modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        string? parentDirectory = Directory.GetParent(modFolder)?.FullName;
        if (parentDirectory == null) return false;
        var filePath = Path.Combine(parentDirectory, modfolder);
        return Directory.Exists(filePath);
    }

    // APBS
    public void RunCompatibilityLayerAPBS()
    {
        var modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        string? parentDirectory = Directory.GetParent(modFolder)?.FullName;
        if (parentDirectory is null) {
            logger.Error($"Something went wrong in going back one level from {modFolder}");
            return;
        }
        var filePath = Path.Combine(parentDirectory, modConfig.APBSFolderName, "blacklists.json");

        var rawBlacklist = modHelper.GetJsonDataFromFile<Dictionary<string, Dictionary<string, HashSet<string>>>>(modFolder, filePath);

        if (rawBlacklist is null)
        {
            logger.Error($"APBS blacklist not found in defauld directory: {filePath}");
            return;
        }

        rawBlacklist.TryGetValue("weaponBlacklist", out var weaponBlacklist);
        if (weaponBlacklist is null)
        {
            logger.Error($"APBS blacklist is incorrect, missing weaponBlacklist property");
            return;
        }

        var maxAPBSTier = 7;
        var blacklistChanged = false;
        for (int i = 1; i <= maxAPBSTier; i++)
        {
            if (!weaponBlacklist.TryGetValue($"tier{i}Blacklist", out var tierBlacklist) || tierBlacklist is null)
            {
                logger.Error($"weaponBlacklist/tier{i}Blacklist is missing");
                continue;
            }

            var weapInTier = new HashSet<string>();
            foreach(var (q, weaps) in modDataStorage.VariantIdsByQuality)
            {
                var qualityBlacklisted = !modConfig.APBSTierConfig.TryGetValue(q, out var tier) || tier == 0 || i < tier;
                foreach (var w in weaps)
                {
                    if (qualityBlacklisted)
                        weapInTier.Add(w);
                    else
                    {
                        if (modConfig.APBSBlacklistedVariantTypes.Contains(modDataStorage.VariantTypes[w]))
                            weapInTier.Add(w);
                    }
                }
            }
            foreach (var weap in tierBlacklist)
            {
                if (weapInTier.Contains(weap) || modDataStorage.AllVariantIds.Contains(weap)) continue;
                if (modDataStorage.Items.ContainsKey(weap))
                {
                    weapInTier.Add(weap);
                }
                else
                {
                    logger.Warning($"Weapon {weap} in weaponBlacklist/tier{i}Blacklist is incorrect - removing");
                }
            }
            if (!weapInTier.SetEquals(tierBlacklist))
            {
                blacklistChanged = true;
                weaponBlacklist[$"tier{i}Blacklist"] = weapInTier;
            }
        }
        if (blacklistChanged)
        {
            string json = jsonUtil.Serialize(rawBlacklist, true);
            File.WriteAllText(filePath, json);
            logger.Error($"APBS blacklist has been updated. Please reload config in APBS web app or restart server!");
        } else
        {
            logger.Ok($"APBS blacklist is up to date");
        }
    }
}
