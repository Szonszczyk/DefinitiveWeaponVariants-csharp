using DefinitiveWeaponVariants.Compatibility;
using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.Generators
{
    internal class WeaponGenerator(
        ISptLogger<DefinitiveWeaponVariants> logger,
        DatabaseService databaseService,
        ModDatabaseLoader modDatabaseLoader,
        IdDatabaseManager idDatabaseManager,
        LocaleService localeService,
        CustomItemCreator customItemCreator,
        CustomPropertiesChanger customPropertiesChanger, 
        CustomSlotsChanger customSlotsChanger,
        ICloner cloner,
        ConfigLoader configLoader,
        ItemHelper itemHelper,
        CustomLootManager customLootManager,
        CompatibilityLayers compatibilityLayers
    )
    {
        private readonly Dictionary<MongoId, TemplateItem> items = databaseService.GetItems();
        private readonly Dictionary<MongoId, Quest> quests = databaseService.GetQuests();
        private readonly Dictionary<MongoId, Trader> traders = databaseService.GetTraders();
        private readonly HandbookBase handbook = databaseService.GetHandbook();
        private readonly Dictionary<string, string> locale = localeService.GetLocaleDb("en");
        private readonly Globals globals = databaseService.GetGlobals();
        private readonly ConfigData modConfig = configLoader.Config;
        private readonly Dictionary<string, string> weaponDescriptions = [];
        private readonly Dictionary<string, List<string>> weaponListForKillQuests = [];
        public void GenerateWeaponsFromVariantConfig()
        {
            
            foreach (var (variantName, config) in modDatabaseLoader.DbVariants)
            {
                if (config is { Description: not null, Explanation: not null, ShortName: not null, Rarity: not null } variant)
                {
                    var weaponsToGenerate = GetAllowedWeaponsToGenerate(variantName, variant);
                    RarityData rarity = RaritySettings.GetByName(variant.Rarity);
                    string weaponNamesInVariant = string.Join(" | ", weaponsToGenerate);
                    foreach (var weaponShortname in weaponsToGenerate)
                    {
                        string variantShortName = $"{weaponShortname} {variant.ShortName}";
                        modDatabaseLoader.DbShortnames.TryGetValue(weaponShortname, out var copiedWeaponId);
                        items.TryGetValue(copiedWeaponId ?? weaponShortname, out var copiedItem);
                        if (copiedItem is null) continue;
                        copiedWeaponId ??= copiedItem.Id;
                        HandbookItem? copiedItemHandbook = handbook.Items.Find(t => t.Id == copiedWeaponId);
                        var copiedItemName = locale[$"{copiedWeaponId} Name"];
                        var weaponCountsToward = copiedItemName;
                        if (variant.WeaponIdToUseAs is not null)
                        {
                            if (customSlotsChanger.GetItemFromString(variant.WeaponIdToUseAs) is not null)
                            {
                                weaponCountsToward = locale[$"{customSlotsChanger.GetItemFromString(variant.WeaponIdToUseAs)?.Id} Name"];
                            }
                        }

                        double? price = copiedItemHandbook!.Price;
                        var newWeapon = new NewItemFromCloneDetails
                        {
                            ItemTplToClone = copiedWeaponId,
                            ParentId = variant.Changes?.Parent != null ? variant.Changes.Parent : copiedItem.Parent, 
                            HandbookParentId = copiedItemHandbook!.ParentId,
                            NewId = idDatabaseManager.GetCustomId($"{weaponShortname}{variant.ShortName}:ID"),
                            FleaPriceRoubles = Math.Ceiling((double)price! * rarity.PriceMultiplier * 2),
                            HandbookPriceRoubles = Math.Ceiling((double)price * rarity.PriceMultiplier),
                            OverrideProperties = new TemplateItemProperties
                            {
                                BackgroundColor = IsPluginLoaded() ? $"{rarity.Color}ff" : rarity.BgColor
                            },
                            Locales = new Dictionary<string, LocaleDetails>
                            {
                                {
                                    "en", new LocaleDetails
                                    {
                                        Name = GenerateVariantName(variant.Rarity, rarity, copiedItemName, variantName),
                                        ShortName = variantShortName,
                                        Description = string.Join("\n", new[] {
                                            $"<align=\"center\">{variant.Description}",
                                            $"",
                                            $"<color={rarity.Color}><b>{variantName} Variant</b></color>",
                                            $"<i>{variant.Explanation}</i>",
                                            $"{weaponNamesInVariant.Replace(weaponShortname, $"<b><color={rarity.Color}>{weaponShortname}</color></b>")}",
                                            $"",
                                            $"<color={rarity.Color}><b>{rarity.StarRating} {variant.Rarity} Quality {rarity.StarRating}</b></color>",
                                            $"<i>{rarity.Flavour}</i>",
                                            $"<color={rarity.Color}>{rarity.Description}</color>",
                                            $"{CreateWeaponDescription(variant)}",
                                            $"This weapon counts toward {weaponCountsToward} kills for quest completion</align>"
                                        })
                                    }
                                }
                            }
                        };
                        // Add mastery
                        CustomItemConfig newWeaponConfig = new();
                        var mastery = globals.Configuration.Mastering.FirstOrDefault(t => t.Templates.Contains(copiedWeaponId));
                        if (mastery != null)
                        {
                            newWeaponConfig.MasteryName = mastery.Name;
                        }

                        if (modConfig.Airdrop[variant.Rarity] == true) newWeaponConfig.AirdropBlacklisted = false;
                        if (modConfig.Fence[variant.Rarity] == true) newWeaponConfig.FenceBlacklisted = false;
                        if (modConfig.Flea[variant.Rarity] == true) newWeaponConfig.FleaBlacklisted = false;

                        // Change normal properties
                        Dictionary<string, object> individualChangesProperties = variant.IndividualChanges?.GetValueOrDefault(weaponShortname)?.Properties ?? [];
                        if (variant.Properties != null || individualChangesProperties != null || variant.Changes?.Minimum != null)
                        {
                            Dictionary<string, object> newProperties = cloner.Clone(variant.Properties) ?? [];
                            // Combine Properties from IndividualChanges with variant config Properties
                            foreach (var kvp in individualChangesProperties!)
                            {
                                newProperties[kvp.Key] = kvp.Value;
                            }
                            // Add default Property if it is in Changes.Minimum but missing in variant config Properties
                            if (variant.Changes?.Minimum != null)
                            {
                                foreach (var (prop, _) in variant.Changes.Minimum)
                                {
                                    if (!newProperties.ContainsKey(prop)) newProperties[prop] = "+0%";
                                }
                            }

                            newWeapon.OverrideProperties = customPropertiesChanger.ChangeItemProperties(newProperties, newWeapon.OverrideProperties, copiedItem, config, variantShortName);
                        }

                        // Add preset
                        Preset? originalPreset =
                            modDatabaseLoader.DbPresets.TryGetValue(weaponShortname, out var value) ? value :
                            modDatabaseLoader.DbPresets.TryGetValue(variantShortName, out var value2) ? value2 : 
                            globals.ItemPresets.Values.FirstOrDefault(p => string.Equals(p.Encyclopedia, copiedWeaponId, StringComparison.OrdinalIgnoreCase));

                        if (originalPreset != null && originalPreset?.Items?.Count > 0)
                        {
                            Preset preset = cloner.Clone(originalPreset)!;
                            preset.Items = itemHelper.ReparentItemAndChildren(preset.Items.First(), preset.Items);
                            var rootItem = preset.Items.First();
                            rootItem.Template = newWeapon.NewId;

                            preset.ChangeWeaponName = false;
                            preset.Encyclopedia = newWeapon.NewId;
                            preset.Id = idDatabaseManager.GetCustomId($"{weaponShortname}{variant.ShortName}:DEFAULTPRESET:ID");
                            preset.Name = $"{copiedItemName} {variantName} Default Preset";
                            preset.Parent = rootItem.Id;

                            foreach (var item in preset.Items)
                            {
                                if (item.Desc is not null)
                                {
                                    var presetItem = customSlotsChanger.GetItemFromString(item.Desc);
                                    if (presetItem is not null)
                                    {
                                        item.Template = presetItem.Id;
                                    } else
                                    {
                                        logger.LogWithColor($"[{GetType().Namespace}] Preset for {variantShortName} have incorrect item: {item.Desc}!", LogTextColor.Yellow);
                                    }
                                    item.Desc = null;
                                }
                            }
                            // change fire mode in preset
                            if (newWeapon.OverrideProperties.WeapFireType is not null)
                            {
                                rootItem.Upd ??= new();
                                rootItem.Upd.FireMode = new()
                                {
                                    FireMode = newWeapon.OverrideProperties.WeapFireType.First()
                                };
                            }
                            newWeaponConfig.Presets[preset.Id] = preset;
                        }
                        else
                        {
                            logger.LogWithColor($"[{GetType().Namespace}] Weapon {copiedItemName} is missing preset so it can't be added to {variantShortName}!", LogTextColor.Yellow);
                        }

                        // Add to inventory slots
                        if (variant.Changes?.AddtoInventorySlots?.Count > 0) newWeaponConfig.AddToInventorySlots = variant.Changes.AddtoInventorySlots;
                        if (weaponShortname.Contains("Sawed-off"))
                            newWeaponConfig.AddToInventorySlots.Add("Holster");
                        else
                        {
                            string Shotgun_ID = "5447b6094bdc2dc3278b4567";
                            string GrenadeLauncher_ID = "5447bedf4bdc2d87278b4568";
                            string Revolver_ID = "617f1ef5e8b54b0998387733";

                            var parentIdsToChange = new[] { Shotgun_ID, GrenadeLauncher_ID, Revolver_ID };

                            if (parentIdsToChange.Contains(newWeapon.ParentId))
                            {
                                if (copiedItem?.Properties?.WeapUseType == "secondary")
                                    newWeaponConfig.AddToInventorySlots.Add("Holster");
                                else
                                {
                                    newWeaponConfig.AddToInventorySlots.Add("FirstPrimaryWeapon");
                                    newWeaponConfig.AddToInventorySlots.Add("SecondPrimaryWeapon");
                                }
                            }
                        }

                        // Change slots
                        var slotConfig = GetCombinedSlotConfig(variant, weaponShortname);
                        var newSlots = customSlotsChanger.SlotsChanger(slotConfig, copiedItem, newWeapon);
                        if (newSlots != null) {
                            newWeapon.OverrideProperties.Slots = newSlots;
                            // Change item in slot in preset(s)
                            if (slotConfig != null)
                            {
                                foreach (var slot in newWeapon.OverrideProperties.Slots)
                                {
                                    if (slot.Name == null) continue;
                                    if (slotConfig.TryGetValue(slot.Name, out FilterSlotExtendedConfiguration? newFilterConfig))
                                    {
                                        var newFilter = slot?.Properties?.Filters?.First().Filter;
                                        if (slot!.Name == "mod_magazine" && newFilter?.Count > 0 && copiedItem?.Properties?.DefMagType != null)
                                        {
                                            newWeapon.OverrideProperties!.DefMagType = newFilter.First();
                                        }
                                        foreach (var (presetId, preset) in newWeaponConfig.Presets)
                                        {
                                            Item? item = preset.Items.FirstOrDefault(t => t.SlotId == slot.Name);
                                            if (item != null)
                                            {
                                                if (newFilter?.Count > 0)
                                                {
                                                    if (!newFilter.Contains(item.Template)) {
                                                        item.Template = newFilter.First();
                                                    }
                                                }
                                                else
                                                {
                                                    preset.Items.Remove(item);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        // Add core slot
                        if (modConfig.VariantCoresEnabled)
                        {
                            var coreTemplate = customSlotsChanger.GetItemFromString($"{variant.Rarity} Quality Variant Core");
                            if (coreTemplate != null)
                            {
                                List<MongoId> newFilterWithCore = [coreTemplate.Id];
                                var newSlotsWithCore = customSlotsChanger.CoreSlotAdder(
                                    newSlots,
                                    copiedItem,
                                    newFilterWithCore,
                                    newWeapon,
                                    modConfig.VariantCores.Required
                                );
                                if (newSlotsWithCore != null)
                                {
                                    newWeapon.OverrideProperties.Slots = newSlotsWithCore;
                                    // Add core to presets
                                    foreach (var (presetId, preset) in newWeaponConfig.Presets)
                                    {
                                        var rootItem = preset.Items.First();
                                        var item = new Item
                                        {
                                            Id = new MongoId(),
                                            Template = coreTemplate.Id,
                                            ParentId = rootItem.Id,
                                            SlotId = "mod_core"
                                        };
                                        preset.Items.Add(item);
                                    }
                                }
                            }
                        }

                        // Change chambers
                        if (variant.Changes?.Chambers != null || variant.IndividualChanges?.GetValueOrDefault(weaponShortname)?.Chambers != null)
                        {
                            var chamberConfig = variant.Changes?.Chambers != null ? variant.Changes.Chambers! : variant.IndividualChanges?.GetValueOrDefault(weaponShortname)?.Chambers!;

                            var newChambers = customSlotsChanger.ChambersChanger(
                                chamberConfig,
                                copiedItem,
                                newWeapon,
                                $"{weaponShortname}{variant.ShortName}"
                            );

                            if (newChambers != null)
                            {
                                newWeapon.OverrideProperties.Chambers = newChambers;
                                var newChamberFilter = newChambers?.First()?.Properties?.Filters?.First().Filter;
                                if (newChamberFilter?.Count > 0)
                                {
                                    var firstId = newChamberFilter.First();
                                    newWeapon.OverrideProperties.DefAmmo = firstId;

                                    if (items.TryGetValue(firstId, out var item) && item?.Properties?.Caliber != null)
                                    {
                                        newWeapon.OverrideProperties.AmmoCaliber = item.Properties.Caliber;
                                    }
                                    else
                                    {
                                        logger.LogWithColor($"[{GetType().Namespace}] Ammo for {variantShortName} is incorrect: {firstId}", LogTextColor.Red);
                                    }
                                }
                                else
                                {
                                    logger.LogWithColor($"[{GetType().Namespace}] Weapon '{variantShortName}' don't have any ammunition allowed in chambers!", LogTextColor.Red);
                                }
                            } else
                            {
                                if (copiedItem?.Properties?.Chambers?.Count() == 0)
                                {
                                    // Weapon don't have chambers - change Defaults
                                    var allowedAmmo = customSlotsChanger.CreateFilterFromConfiguration(chamberConfig, "N/A", "Chambers", copiedItem);
                                    var firstId = allowedAmmo.First();
                                    newWeapon.OverrideProperties.DefAmmo = firstId;
                                    if (items.TryGetValue(firstId, out var item) && item?.Properties?.Caliber != null)
                                    {
                                        newWeapon.OverrideProperties.AmmoCaliber = item.Properties.Caliber;
                                    }
                                    else
                                    {
                                        logger.LogWithColor($"[{GetType().Namespace}] Ammo for {variantShortName} is incorrect: {firstId}", LogTextColor.Red);
                                    }
                                } else
                                {
                                    // Weapon have chambers - but were not changed
                                    logger.LogWithColor($"[{GetType().Namespace}] Chambers in weapon '{variantShortName}' were not changed - unknown error!", LogTextColor.Red);
                                }
                            }
                        }
                        // Add weapon to loot databse
                        if (newWeaponConfig.Presets.Count > 0)
                        {
                            customLootManager.AddVariantToLootDatabase(newWeaponConfig.Presets.First().Value.Items, variant.Rarity);
                        }

                        // Add weapon to weapon list for kill quests database
                        var weaponIdToUseAs = variant.WeaponIdToUseAs ?? copiedItem?.Id;
                        if (weaponIdToUseAs is not null && weaponListForKillQuests.TryGetValue(weaponIdToUseAs, out var list))
                        {
                            list.Add(newWeapon.NewId);
                            
                        }
                        else
                        {
                            if (weaponIdToUseAs is not null)
                            {
                                weaponListForKillQuests.Add(weaponIdToUseAs, [newWeapon.NewId]);
                            }
                        }
                        compatibilityLayers.AddVariantToDB(newWeapon.NewId, variant.Rarity, variantName);
                        if (config.Barter is not null && modConfig.AmonyaTraderMode) config.Barter.TraderId = "ee840a5ba014e9c5478d5ccd";
                        customItemCreator.AddItemToDatabase(newWeapon, newWeaponConfig, config.Barter ?? new CustomBarterConfig());
                    }
                }
            }
            AddVariantsToKillQuests();
        }
        private static bool IsPluginLoaded()
        {
            const string pluginName = "rairai.colorconverterapi.dll";
            const string pluginsPath = "../BepInEx/plugins";

            try
            {
                if (!Directory.Exists(pluginsPath))
                    return false;

                var pluginList = Directory.GetFiles(pluginsPath)
                    .Select(System.IO.Path.GetFileName)
                    .Select(f => f?.ToLowerInvariant());
                return pluginList.Contains(pluginName);
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, FilterSlotExtendedConfiguration>? GetCombinedSlotConfig(
            VariantConfiguration variant,
            string weaponShortname
        )
        {
            // Check if either slot source exists
            var changeSlots = variant.Changes?.Slots;
            var individualSlots = variant.IndividualChanges?.GetValueOrDefault(weaponShortname)?.Slots;

            if (changeSlots == null && individualSlots == null)
                return null;

            // If both exist → merge
            if (changeSlots != null && individualSlots != null)
            {
                var combined = new Dictionary<string, FilterSlotExtendedConfiguration>(changeSlots);
                foreach (var kvp in individualSlots)
                {
                    combined[kvp.Key] = kvp.Value; // replace or add
                }
                return combined;
            }

            // If only one exists → return its copy
            if (changeSlots != null)
                return new Dictionary<string, FilterSlotExtendedConfiguration>(changeSlots);

            return new Dictionary<string, FilterSlotExtendedConfiguration>(individualSlots!);
        }

        private static string GenerateVariantName(string? rarityName, RarityData rarity, string copiedItemName, string variantName)
        {
            if (rarityName == "Unique")
            {
                string variantFullName = $"{copiedItemName} Unique";
                return $"<b>{RainbowText.RainbowUnityRichText(variantFullName)}</b>";
            }

            return $"<b><color={rarity.Color}>{copiedItemName} {variantName}</color></b>";
        }

        private List<string> GetAllowedWeaponsToGenerate(string variantName, VariantConfiguration variant)
        {
            var modConfig = configLoader.Config;
            if (modConfig.NotGenerateVariantTypes.Contains(variantName)) return [];

            if (variant.Rarity == null || RaritySettings.GetByName(variant.Rarity) == null) {
                logger.LogWithColor($"[{GetType().Namespace}] Rarity of {variantName} is missing or is incorrect: {variant.Rarity}", LogTextColor.Red);
                return [];
            }
            if (!modConfig.Generate[variant.Rarity]) return [];

            var weaponsToGenerate = new List<string>();
            foreach(var weaponShortname in variant.Weapons) 
            {
                var variantShortName = $"{weaponShortname} {variant.ShortName}";
                if (modConfig.NotGenerateWeapons.Contains(variantShortName)) continue;

                modDatabaseLoader.DbShortnames.TryGetValue(weaponShortname, out var copiedWeaponId);
                if (copiedWeaponId is null)
                {
                    if (items.TryGetValue(weaponShortname, out var item)) copiedWeaponId = item.Id;
                }
                if (string.IsNullOrEmpty(copiedWeaponId))
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Weapon {weaponShortname} is missing shortname in db/03_Shortnames (or is incorrect)", LogTextColor.Red);
                    continue;
                }
                items.TryGetValue(copiedWeaponId, out var copiedItem);
                if (copiedItem == null)
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Base weapon '{weaponShortname}/{copiedWeaponId}' not found. Skipping", LogTextColor.Yellow);
                    continue;
                }
                HandbookItem? copiedItemHandbook = handbook.Items.Find(t => t.Id == copiedWeaponId);
                if (copiedItemHandbook == null)
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Handbook entry for '{weaponShortname}/{copiedWeaponId}' not found. Skipping", LogTextColor.Yellow);
                    continue;
                }
                weaponsToGenerate.Add(weaponShortname);
            }

            // remove duplicates
            return [.. weaponsToGenerate.Distinct()];
        }
        private string CreateWeaponDescription(VariantConfiguration config)
        {
            string rarity = config.Rarity!;
            
            if (!weaponDescriptions.TryGetValue(rarity, out _))
            {
                List<string> strings = [];
                if (modConfig.Airdrop[rarity]) strings.Add("in Airdrop");
                if (modConfig.Fence[rarity]) strings.Add("in Fence");
                if (modConfig.Flea[rarity]) strings.Add("on Flea Market");
                if (modConfig.Marked[rarity] && modConfig.MarkedRoomsProbability > 0) strings.Add("in Marked Rooms (Customs, Reserve, Streets)");
                if (modConfig.StaticLoot[rarity] && modConfig.StaticLootProbability > 0) strings.Add("in Weapon Boxes, Duffle Bags, Wooden Crates and Caches");
                if (modConfig.BlindBoxesEnabled && modConfig.VariantCores.Price.TryGetValue(rarity, out _) && modConfig.BlindBoxes.Price[rarity] > 0) strings.Add($"in {rarity} Weapon Variant Blind Box");
                if (modConfig.EnableAPBSBlacklistGeneration && modConfig.APBSTierConfig[rarity] > 0) strings.Add($"on enemies of {modConfig.APBSTierConfig[rarity]}-7 tiers");
                weaponDescriptions[rarity] = strings.Count > 0 ? $"Weapons of this variant type can be found: {string.Join(", ", strings)}" : "";
            }
            if (config.Barter is not null)
            {
                var traderName = customItemCreator.GetTraderIdByName(config.Barter.TraderId) == null ? "N/A" : traders[(MongoId)customItemCreator.GetTraderIdByName(config.Barter.TraderId)!].Base.Nickname;
                return $"{weaponDescriptions[rarity]}/nCan be bought in {traderName} LL{config.Barter.LoyalLevel}";
            }

            return weaponDescriptions[rarity];
        }

        private void AddVariantsToKillQuests()
        {
            foreach (var (_, quest) in quests)
            {
                var affs = quest.Conditions.AvailableForFinish;
                if (affs is null) continue;
                foreach (var aff in affs)
                {
                    var affConditions = aff?.Counter?.Conditions;
                    if (affConditions is null) continue;
                    
                    foreach(var affCondition in affConditions)
                    {
                        var weaponsInQuest = affCondition.Weapon;
                        if (weaponsInQuest is null) continue;
                        List<string> mongoIds = [.. weaponsInQuest];
                        foreach (var weaponId in weaponsInQuest)
                        {
                            if (weaponListForKillQuests.TryGetValue(weaponId, out var weaponList))
                            {
                                mongoIds.AddRange(weaponList);
                            }
                        }
                        affCondition.Weapon = [.. mongoIds.Distinct()];
                    }
                }
                
            }
        }
    }
}