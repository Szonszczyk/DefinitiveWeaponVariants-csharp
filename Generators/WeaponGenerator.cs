using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Integrations;
using DefinitiveWeaponVariants.Loaders;
using DefinitiveWeaponVariants.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.Generators;

[Injectable(InjectionType.Singleton)]
public class WeaponGenerator(
    CustomLogger logger,
    ModDatabaseLoader modDatabaseLoader,
    IdDatabaseManager idDatabaseManager,
    CustomItemCreator customItemCreator,
    CustomPropertiesChanger customPropertiesChanger,
    CustomSlotsChanger customSlotsChanger,
    ICloner cloner,
    ConfigData config,
    ItemHelper itemHelper,
    ModDataStorage modDataStorage,
    GlobalTable globalTable,
    TemplateTable templateTable,
    LocaleService localeService,
    CustomLocales customLocales,
    ModCheck modCheck
)
{
    private readonly ConfigData modConfig = config;
    private readonly Dictionary<string, string> weaponDescriptions = [];
    private readonly Dictionary<string, List<string>> weaponListForKillQuests = [];

    public void GenerateWeaponsFromVariantConfig()
    {
        var LocaleEn = localeService.GetLocaleDb("en");
        foreach (var (variantName, config) in modDatabaseLoader.DbVariants)
        {
            if (config is { Description: not null, Explanation: not null, ShortName: not null, Rarity: not null } variant)
            {
                foreach (var modGUID in variant.ModGUIDs ?? []) if (!modCheck.CheckInstalledMod(modGUID)) continue; // Mod check

                var rarity = RaritySettings.GetByName(variant.Rarity);
                var weaponsToGenerate = GetAllowedWeaponsToGenerate(variantName, variant);

                var weaponNamesInVariant = $"{{{string.Join(" ShortName} | {", weaponsToGenerate)} ShortName}}";
                foreach (var copiedWeaponId in weaponsToGenerate)
                {
                    var internalShortname = modDatabaseLoader.DbShortnames.FirstOrDefault(x => x.Value == copiedWeaponId).Key;
                    if (internalShortname == null) LocaleEn.TryGetValue($"{copiedWeaponId} ShortName", out internalShortname);
                    internalShortname ??= copiedWeaponId;

                    templateTable.Items.TryGetValue(copiedWeaponId, out var copiedItem);
                    if (copiedItem is null) continue;
                    HandbookItem? copiedItemHandbook = templateTable.Handbook.Items.Find(t => t.Id == copiedWeaponId);
                    var weaponCountsToward = $"{{{copiedWeaponId} Name}}";
                    if (variant.WeaponIdToUseAs != null && customSlotsChanger.GetItemFromString(variant.WeaponIdToUseAs) != null)
                    {
                        weaponCountsToward = $"{{{customSlotsChanger.GetItemFromString(variant.WeaponIdToUseAs)?.Id} Name}}";
                    }
                    double? price = copiedItemHandbook!.Price;
                    var newWeapon = new NewItemFromCloneDetails
                    {
                        NewItemName = $"{internalShortname} {variant.ShortName}",
                        ItemTplToClone = copiedWeaponId,
                        ParentId = variant.Changes?.Parent != null ? variant.Changes.Parent : copiedItem.Parent,
                        HandbookParentId = copiedItemHandbook!.ParentId,
                        NewId = idDatabaseManager.GetCustomId($"{internalShortname}{variant.ShortName}:ID"),
                        FleaPriceRoubles = Math.Ceiling((double)price! * rarity.PriceMultiplier * 2),
                        HandbookPriceRoubles = Math.Ceiling((double)price * rarity.PriceMultiplier),
                        OverrideProperties = new TemplateItemProperties
                        {
                            BackgroundColor = ModDataStorage.IsPluginLoaded() ? $"{rarity.Color}ff" : rarity.BgColor
                        },
                        Locales = []
                    };
                    customLocales.RegisterTag("Quality", $"{{Quality.{variant.Rarity}.Name}}");
                    customLocales.RegisterTag("APBSMinTier", $"{{APBSMinTier.{variant.Rarity}}}");
                    customLocales.RegisterTag("WeaponCountsToward", weaponCountsToward);
                    string localizedName = $"{{{copiedWeaponId} Name}} {{{variantName}.Name}}";
                    localizedName = variant.Rarity == "Unique"
                        ? $"<b><dwv-rainbow>{localizedName}</dwv-rainbow></b>"
                        : $"<b><color={rarity.Color}>{localizedName}</color></b>";
                    newWeapon.Locales = customLocales.CreateItemLocale(
                        localizedName,
                        $"{{{copiedWeaponId} ShortName}} {{{variantName}.ShortName}}",
                        string.Join("\n", new[] {
                            $"<align=\"center\">{{{variantName}.Description}}",
                            $"",
                            $"<color={rarity.Color}><b>{{{variantName}.Name}} {{Other.Word.Variant}}</b></color>",
                            $"<i>{{{variantName}.Explanation}}</i>",
                            $"{weaponNamesInVariant.Replace($"{{{copiedWeaponId} ShortName}}", $"<b><color={rarity.Color}>{{{copiedWeaponId} ShortName}}</color></b>")}",
                            $"",
                            $"<color={rarity.Color}><b>{rarity.StarRating} {variant.Rarity} {{Other.Word.Quality}} {rarity.StarRating}</b></color>",
                            $"<i>{rarity.Flavour}</i>",
                            $"<color={rarity.Color}>{rarity.Description}</color>",
                            $"{CreateWeaponDescription(variant)}",
                            "{Desc.CountToward}</align>"
                        }),
                        newWeapon.NewId
                    );


                    // Add mastery
                    CustomItemConfig newWeaponConfig = new();
                    var mastery = globalTable.Configuration.Mastering.FirstOrDefault(t => t.Templates.Contains(copiedWeaponId));
                    if (mastery != null)
                    {
                        newWeaponConfig.MasteryName = mastery.Name;
                    }

                    if (modConfig.Airdrop[variant.Rarity] == true) newWeaponConfig.AirdropBlacklisted = false;
                    if (modConfig.Fence[variant.Rarity] == true) newWeaponConfig.FenceBlacklisted = false;
                    if (modConfig.Flea[variant.Rarity] == true) newWeaponConfig.FleaBlacklisted = false;

                    // Change normal properties
                    Dictionary<string, object> individualChangesProperties = variant.IndividualChanges?.GetValueOrDefault(internalShortname)?.Properties ?? [];
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

                        newWeapon.OverrideProperties = customPropertiesChanger.ChangeItemProperties(newProperties, newWeapon.OverrideProperties, copiedItem, config, newWeapon.NewItemName);
                    }

                    // Add preset
                    Preset? originalPreset =
                        modDatabaseLoader.DbPresets.TryGetValue(newWeapon.NewItemName, out var value) ? value :
                        modDatabaseLoader.DbPresets.TryGetValue(internalShortname, out var value2) ? value2 :
                        globalTable.ItemPresets.Values.FirstOrDefault(p => string.Equals(p.Encyclopedia, copiedWeaponId, StringComparison.OrdinalIgnoreCase));

                    if (originalPreset != null && originalPreset?.Items?.Count > 0)
                    {
                        Preset preset = cloner.Clone(originalPreset)!;
                        preset.Items = itemHelper.ReparentItemAndChildren(preset.Items.First(), preset.Items);
                        var rootItem = preset.Items.First();
                        rootItem.Template = newWeapon.NewId;

                        preset.ChangeWeaponName = false;
                        preset.Encyclopedia = newWeapon.NewId;
                        preset.Id = idDatabaseManager.GetCustomId($"{internalShortname}{variant.ShortName}:DEFAULTPRESET:ID");
                        preset.Name = $"{LocaleEn[$"{copiedWeaponId} Name"]} {variantName} Default Preset";
                        preset.Parent = rootItem.Id;

                        foreach (var item in preset.Items)
                        {
                            if (item.Desc is not null)
                            {
                                var presetItem = customSlotsChanger.GetItemFromString(item.Desc);
                                if (presetItem is not null)
                                {
                                    item.Template = presetItem.Id;
                                }
                                else
                                {
                                    logger.Warning($"Preset for {newWeapon.NewItemName} have incorrect item: {item.Desc}!");
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
                        logger.Warning($"Weapon {LocaleEn[$"{copiedWeaponId} Name"]} is missing preset so it can't be added to {newWeapon.NewItemName}!");
                    }

                    // Add to inventory slots
                    if (variant.Changes?.AddtoInventorySlots?.Count > 0) newWeaponConfig.AddToInventorySlots = variant.Changes.AddtoInventorySlots;
                    if (internalShortname.Contains("Sawed-off"))
                        newWeaponConfig.AddToInventorySlots.Add("Holster");
                    else
                    {
                        string Shotgun_ID = "5447b6094bdc2dc3278b4567";
                        string GrenadeLauncher_ID = "5447bedf4bdc2d87278b4568";
                        string Revolver_ID = "617f1ef5e8b54b0998387733";

                        var parentIdsToChange = new[] { Shotgun_ID, GrenadeLauncher_ID, Revolver_ID };

                        if (parentIdsToChange.Contains(newWeapon.ParentId))
                        {
                            if (copiedItem.Properties?.WeapUseType == "secondary")
                                newWeaponConfig.AddToInventorySlots.Add("Holster");
                            else
                            {
                                newWeaponConfig.AddToInventorySlots.Add("FirstPrimaryWeapon");
                                newWeaponConfig.AddToInventorySlots.Add("SecondPrimaryWeapon");
                            }
                        }
                    }

                    // Change slots
                    var slotConfig = GetCombinedSlotConfig(variant, internalShortname);
                    var newSlots = customSlotsChanger.SlotsChanger(slotConfig, copiedItem, newWeapon);
                    if (newSlots != null)
                    {
                        newWeapon.OverrideProperties.Slots = newSlots;
                        // Change item in slot in preset(s)
                        if (slotConfig != null)
                        {
                            foreach (var slotName in slotConfig.Keys)
                            {
                                var slot = newSlots.FirstOrDefault(s => s.Name == slotName);
                                var newFilter = slot?.Properties?.Filters?.FirstOrDefault()?.Filter;
                                if (slotName == "mod_magazine" && newFilter?.Count > 0 && copiedItem.Properties?.DefMagType != null)
                                {
                                    newWeapon.OverrideProperties!.DefMagType = newFilter.First();
                                }
                                foreach (var preset in newWeaponConfig.Presets.Values)
                                {
                                    var item = preset.Items.FirstOrDefault(i => i.SlotId == slotName);
                                    if (item == null) continue;
                                    if (newFilter?.Count > 0)
                                    {
                                        if (!newFilter.Contains(item.Template)) item.Template = newFilter.First();
                                    }
                                    else {
                                        // Remove the attachment and its descendants.
                                        var removedIds = new HashSet<MongoId> { item.Id };
                                        bool foundChildren;
                                        do {
                                            foundChildren = false;
                                            foreach (var child in preset.Items)
                                            {
                                                if (child.ParentId != null
                                                    && removedIds.Any(id => id.ToString() == child.ParentId)
                                                    && removedIds.Add(child.Id))
                                                {
                                                    foundChildren = true;
                                                }
                                            }
                                        } while (foundChildren);
                                        preset.Items.RemoveAll(i => removedIds.Contains(i.Id));
                                    }
                                }
                            }
                        }
                    }
                    // Add core slot
                    if (modConfig.VariantCoresEnabled)
                    {
                        var newSlotsWithCore = customSlotsChanger.CoreSlotAdder(
                            newSlots,
                            copiedItem,
                            variant.Rarity,
                            newWeapon,
                            modConfig.VariantCores.General.Required
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
                                    Template = modDataStorage.CoresByQuality[variant.Rarity].First(),
                                    ParentId = rootItem.Id,
                                    SlotId = "mod_core"
                                };
                                preset.Items.Add(item);
                            }
                        }
                    }

                    // Change chambers
                    if (variant.Changes?.Chambers != null || variant.IndividualChanges?.GetValueOrDefault(internalShortname)?.Chambers != null)
                    {
                        var chamberConfig = variant.Changes?.Chambers != null ? variant.Changes.Chambers! : variant.IndividualChanges?.GetValueOrDefault(internalShortname)?.Chambers!;

                        var newChambers = customSlotsChanger.ChambersChanger(
                            chamberConfig,
                            copiedItem,
                            newWeapon,
                            $"{internalShortname}{variant.ShortName}"
                        );

                        if (newChambers != null)
                        {
                            newWeapon.OverrideProperties.Chambers = newChambers;
                            var newChamberFilter = newChambers?.First()?.Properties?.Filters?.First().Filter;
                            if (newChamberFilter?.Count > 0)
                            {
                                var firstId = newChamberFilter.First();
                                newWeapon.OverrideProperties.DefAmmo = firstId;

                                if (templateTable.Items.TryGetValue(firstId, out var item) && item?.Properties?.Caliber != null)
                                {
                                    newWeapon.OverrideProperties.AmmoCaliber = item.Properties.Caliber;
                                }
                                else
                                {
                                    logger.Error($"Ammo for {newWeapon.NewItemName} is incorrect: {firstId}");
                                }
                            }
                            else
                            {
                                logger.Error($"Weapon '{newWeapon.NewItemName}' don't have any ammunition allowed in chambers!");
                            }
                        }
                        else
                        {
                            if (copiedItem?.Properties?.Chambers?.Count() == 0)
                            {
                                // Weapon don't have chambers - change Defaults
                                var allowedAmmo = customSlotsChanger.CreateFilterFromConfiguration(chamberConfig, "N/A", "Chambers", copiedItem);
                                var firstId = allowedAmmo.First();
                                newWeapon.OverrideProperties.DefAmmo = firstId;
                                if (templateTable.Items.TryGetValue(firstId, out var item) && item?.Properties?.Caliber != null)
                                {
                                    newWeapon.OverrideProperties.AmmoCaliber = item.Properties.Caliber;
                                }
                                else
                                {
                                    logger.Error($"Ammo for {newWeapon.NewItemName} is incorrect: {firstId}");
                                }
                            }
                            else
                            {
                                // Weapon have chambers - but were not changed
                                logger.Error($"Chambers in weapon '{newWeapon.NewItemName}' were not changed - unknown error!");
                            }
                        }
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
                    modDataStorage.AddVariantToStorage(newWeapon.NewId, variant.Rarity, variantName, newWeaponConfig.Presets);

                    modDataStorage.CoreIDsByQuality.TryGetValue(variant.Rarity, out var coreID);

                    if (modConfig.QualityWeights.TryGetValue(variant.Rarity, out var qualityWeight))
                    {
                        var barter = coreID == null ? null : new CustomBarterConfig()
                        {
                            LoyalLevel = 1,
                            UnlimitedCount = false,
                            StackObjectsCount = 1,
                            BarterPrice = { [coreID] = 3 }
                        };
                        barter?.RandomAssortWeight = qualityWeight;
                        customItemCreator.AddPresetToTrader(newWeapon.NewId, newWeaponConfig.Presets.FirstOrDefault().Value.Items, barter ?? new CustomBarterConfig());
                    }
                    customItemCreator.AddItemToDatabase(newWeapon, newWeaponConfig, new CustomBarterConfig());
                    modDataStorage.AddItemToQuality(newWeapon.NewId, variant.Rarity);
                    //if (variant.Rarity == "Unique")
                    //    customItemCreator.CreateCultistCircleCraft(
                    //        [newWeapon.NewId],
                    //        [copiedWeaponId, idDatabaseManager.GetCustomId($"{variant.Rarity} Quality Variant Core (Locked):ID")],
                    //        10,
                    //        true
                    //    );
                }
            }
        }
        AddVariantsToKillQuests();
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

    private HashSet<string> GetAllowedWeaponsToGenerate(string variantName, VariantConfiguration variant)
    {
        if (modConfig.NotGenerateVariantTypes.Contains(variantName)) return [];

        if (variant.Rarity == null || RaritySettings.GetByName(variant.Rarity) == null)
        {
            logger.Error($"Rarity of {variantName} is missing or is incorrect: {variant.Rarity}");
            return [];
        }
        if (!modConfig.Generate[variant.Rarity]) return [];

        var weaponsToCheck = cloner.Clone(variant.Weapons);
        if (weaponsToCheck == null) return [];
        foreach(var (weapon, modGUID) in variant.ModWeapons) if (modCheck.CheckInstalledMod(modGUID)) weaponsToCheck.Add(weapon); // Weapon mod check

        var weaponsToGenerate = new HashSet<string>();
        foreach (var weaponShortname in weaponsToCheck)
        {
            var variantShortName = $"{weaponShortname} {variant.ShortName}";
            if (modConfig.NotGenerateWeapons.Contains(variantShortName)) continue;

            modDatabaseLoader.DbShortnames.TryGetValue(weaponShortname, out var copiedWeaponId);
            if (copiedWeaponId is null && weaponShortname.IsValidMongoId())
            {
                if (templateTable.Items.TryGetValue(weaponShortname, out var item)) copiedWeaponId = item.Id;
            }
            if (string.IsNullOrEmpty(copiedWeaponId))
            {
                logger.Error($"Weapon {weaponShortname} is missing shortname in db/03_Shortnames (or is incorrect)");
                continue;
            }
            templateTable.Items.TryGetValue(copiedWeaponId, out var copiedItem);
            if (copiedItem == null)
            {
                logger.Warning($"Base weapon '{weaponShortname}/{copiedWeaponId}' not found. Skipping");
                continue;
            }
            HandbookItem? copiedItemHandbook = templateTable.Handbook.Items.Find(t => t.Id == copiedWeaponId);
            if (copiedItemHandbook == null)
            {
                logger.Warning($"Handbook entry for '{weaponShortname}/{copiedWeaponId}' not found. Skipping");
                continue;
            }
            weaponsToGenerate.Add(copiedWeaponId);
        }
        return weaponsToGenerate;
    }
    private string CreateWeaponDescription(VariantConfiguration config)
    {
        string rarity = config.Rarity!;

        if (!weaponDescriptions.TryGetValue(rarity, out _))
        {
            List<string> strings = [];
            if (modConfig.Airdrop[rarity]) strings.Add("{Desc.Airdrop}");
            if (modConfig.Fence[rarity]) strings.Add("{Desc.Fence}");
            if (modConfig.Flea[rarity]) strings.Add("{Desc.Flea}");
            if (modConfig.Marked[rarity] && modConfig.MarkedRoomsProbability > 0) strings.Add("{Desc.Marked}");
            if (modConfig.StaticLoot[rarity] && modConfig.StaticLootProbability > 0) strings.Add("{Desc.StaticLoot}");
            if (modConfig.BlindBoxesEnabled && modConfig.VariantCores.General.Price.TryGetValue(rarity, out _)) strings.Add("{Desc.BlindBox}"); // {Rarity} tag
            if (modConfig.EnableAPBSBlacklistGeneration && modConfig.APBSTierConfig[rarity] > 0)
            {
                customLocales.RegisterTag($"APBSMinTier.{rarity}", modConfig.APBSTierConfig[rarity].ToString());
                strings.Add("{Desc.APBS}"); // {APBSMinTier} tag
            }
            weaponDescriptions[rarity] = strings.Count > 0 ? $"{{Desc.Start}}: {string.Join(", ", strings)}" : "";
        }
        if (config.Barter != null && customItemCreator.GetTraderIdByName(config.Barter.TraderId) != null)
        {
            return $"{weaponDescriptions[rarity]}/n{{Desc.Barter}} {{{config.Barter.TraderId} Nickname}} LL{config.Barter.LoyalLevel}";
        }

        return weaponDescriptions[rarity];
    }

    private void AddVariantsToKillQuests()
    {
        foreach (var (_, quest) in templateTable.Quests)
        {
            var affs = quest.Conditions.AvailableForFinish;
            if (affs is null) continue;
            foreach (var aff in affs)
            {
                var affConditions = aff?.Counter?.Conditions;
                if (affConditions is null) continue;

                foreach (var affCondition in affConditions)
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
