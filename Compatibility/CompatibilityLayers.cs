using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using System.Reflection;
using Path = System.IO.Path;

namespace DefinitiveWeaponVariants.Compatibility
{
    internal class CompatibilityLayers(
        ISptLogger<DefinitiveWeaponVariants> logger,
        ConfigLoader configLoader,
        ModHelper modHelper,
        JsonUtil jsonUtil,
        DatabaseService databaseService,
        IReadOnlyList<SptMod> modlist
    )
    {
        private readonly Dictionary<MongoId, TemplateItem> items = databaseService.GetItems();
        private readonly ConfigData modConfig = configLoader.Config;

        private readonly Dictionary<string, HashSet<string>> variants = [];
        private readonly Dictionary<string, string> variantTypes = [];
        private readonly HashSet<string> allVariants = [];
        public void AddVariantToDB(string id, string quality, string variantType)
        {
            variants.TryGetValue(quality, out var variantList);
            if (variantList != null)
            {
                variantList.Add(id);
            }
            else
            {
                variants[quality] = [id];
            }
            variantTypes.Add(id, variantType);
            allVariants.Add(id);
        }

        public void RunCompatibilityLayers()
        {
            if (modConfig.EnableAPBSBlacklistGeneration) RunCompatibilityLayerAPBS();
        }

        public void CheckAllMods()
        {
            if (modConfig.AmonyaTraderMode)
            {
                if (!ModCheck("com.szonszczyk.amonya"))
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Config option \"AmonyaTraderMode\" was enabled but Amonya mod is missing. Please download: https://forge.sp-tarkov.com/mod/2419/amonya-ammo-loving-trader-quester", LogTextColor.Yellow);
                    modConfig.AmonyaTraderMode = false;
                } else
                {
                    modConfig.SpecialAmmoBuyableEnabled = false;
                }
            } 
            if (modConfig.EnableAPBSBlacklistGeneration && !ModCheck("com.acidphantasm.progressivebotsystem", modConfig.APBSFolderName))
            {
                logger.LogWithColor($"[{GetType().Namespace}] Config option \"EnableAPBSBlacklistGeneration\" was enabled but APBS mod is missing (or APBS folder was not found). Please download: https://forge.sp-tarkov.com/mod/1594/apbs-acids-progressive-bot-system", LogTextColor.Yellow);
                modConfig.EnableAPBSBlacklistGeneration = false;
            }
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
                logger.LogWithColor($"[{GetType().Namespace}/APBS] Something went wrong in going back one level from {modFolder}", LogTextColor.Red);
                return;
            }
            var filePath = Path.Combine(parentDirectory, modConfig.APBSFolderName, "blacklists.json");

            var rawBlacklist = modHelper.GetJsonDataFromFile<Dictionary<string, Dictionary<string, HashSet<string>>>>(modFolder, filePath);

            if (rawBlacklist is null)
            {
                logger.LogWithColor($"[{GetType().Namespace}/APBS] APBS blacklist not found in defauld directory: {filePath}", LogTextColor.Red);
                return;
            }

            rawBlacklist.TryGetValue("weaponBlacklist", out var weaponBlacklist);
            if (weaponBlacklist is null)
            {
                logger.LogWithColor($"[{GetType().Namespace}/APBS] APBS blacklist is incorrect, missing weaponBlacklist property", LogTextColor.Red);
                return;
            }

            var maxAPBSTier = 7;
            var blacklistChanged = false;
            for (int i = 1; i <= maxAPBSTier; i++)
            {
                if (!weaponBlacklist.TryGetValue($"tier{i}Blacklist", out var tierBlacklist) || tierBlacklist is null)
                {
                    logger.LogWithColor($"[{GetType().Namespace}/APBS] weaponBlacklist/tier{i}Blacklist is missing", LogTextColor.Red);
                    continue;
                }

                var weapInTier = new HashSet<string>();
                foreach(var (q, weaps) in variants)
                {
                    var qualityBlacklisted = !modConfig.APBSTierConfig.TryGetValue(q, out var tier) || tier == 0 || i < tier;
                    foreach (var w in weaps)
                    {
                        if (qualityBlacklisted)
                            weapInTier.Add(w);
                        else
                        {
                            if (modConfig.APBSBlacklistedVariantTypes.Contains(variantTypes[w]))
                                weapInTier.Add(w);
                        }
                    }
                }
                foreach (var weap in tierBlacklist)
                {
                    if (weapInTier.Contains(weap) || allVariants.Contains(weap)) continue;
                    if (items.ContainsKey(weap))
                    {
                        weapInTier.Add(weap);
                    }
                    else
                    {
                        logger.LogWithColor($"[{GetType().Namespace}/APBS] Weapon {weap} in weaponBlacklist/tier{i}Blacklist is incorrect - removing", LogTextColor.Yellow);
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
                logger.LogWithColor($"[{GetType().Namespace}/APBS] APBS blacklist has been updated. Please reload config in APBS web app or restart server!", LogTextColor.Red, LogBackgroundColor.White);
            } else
            {
                logger.LogWithColor($"[{GetType().Namespace}/APBS] APBS blacklist is up to date", LogTextColor.Green);
            }
        }
    }
}