using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.Generators
{
    internal class ItemGenerator(
        ISptLogger<DefinitiveWeaponVariants> logger,
        DatabaseService databaseService,
        ModDatabaseLoader modDatabaseLoader,
        IdDatabaseManager idDatabaseManager,
        CustomItemCreator customItemCreator,
        CustomPropertiesChanger customPropertiesChanger,
        CustomSlotsChanger customSlotsChanger,
        ConfigLoader configLoader,
        ICloner cloner
    )
    {
        private readonly Dictionary<MongoId, TemplateItem> items = databaseService.GetItems();
        private readonly HandbookBase handbook = databaseService.GetHandbook();
        private readonly Dictionary<MongoId, Trader> traders = databaseService.GetTraders();
        private readonly ConfigData modConfig = configLoader.Config;

        public void GenerateAllItems()
        {
            var variantCorePropertiesOverride = new TemplateItemProperties
            {
                StackMaxSize = 10,
                Prefab = new Prefab
                {
                    Path = "assets/content/items/barter/cpu/item_cpu.bundle",
                    Rcid = ""
                },
                Ergonomics = modConfig.VariantCores.Ergonomics,
                ExtraSizeDown = 0,
                ToolModdable = true,
                RaidModdable = true,
                Recoil = 0,
                Slots = []
            };

            // Generate all Variant Cores
            var unknownPackageLootpool = new Dictionary<string, int>();
            foreach (var (quality, enabled) in modConfig.Generate)
            {
                var variant = new VariantConfiguration
                {
                    Description = "A small, flat chip installed in Variant Weapons. It provides minor ergonomic benefits and serves as a standard internal component",
                    ShortName = $"{RaritySettings.GetByName(quality).ShortName} Core",
                    ItemTplToClone = "58d2912286f7744e27117493",
                    HandbookPriceRoubles = modConfig.VariantCores.Price[quality],
                    Rarity = quality,
                    VariantType = quality
                };
                if (enabled && modConfig.VariantCores.Buyable)
                {
                    variant.Barter = modConfig.VariantCores.CoresBarter;
                    variant.Barter.BarterPrice = [];
                    variant.Barter.BarterPrice.Add("5449016a4bdc2d6f028b456f", modConfig.VariantCores.Price[quality]);
                }
                var newId = GenerateItem(
                    $"{quality} Quality Variant Core",
                    variant,
                    "<color={rarity.Color}><b>{quality} Quality Variant Core</b></color>",
                    $"A core component used in Variant Weapons. Provides a small ergonomics boost and can be used to purchase Variant Blind Boxes. Can be inserted into a compatible slot in a weapon variant of the same quality.{(modConfig.VariantCores.FoundOnEnemies.TryGetValue("assault", out _) ? $"\nCan be found in Scav pockets" : "")}",
                    variantCorePropertiesOverride
                );
                if (newId is null) { continue; }
                if (customSlotsChanger.coreMods.TryGetValue(quality, out var coreMods))
                {
                    coreMods.Add(newId);
                }
                else { customSlotsChanger.coreMods[quality] = [newId]; }
                RarityData rarity = RaritySettings.GetByName(variant.Rarity);
                unknownPackageLootpool.Add($"<color={rarity.Color}><b>{quality} Quality Variant Core</b></color>", modConfig.QualityWeights[quality] * 2);
            }
            // Generate all Variant Cores (Locked)
            variantCorePropertiesOverride.Ergonomics = modConfig.VariantCores.LockedCoresErgonomics;
            foreach (var (quality, enabled) in modConfig.Generate)
            {
                var variant = new VariantConfiguration
                {
                    Description = "A small, flat chip installed in Variant Weapons. This locked version offers improved ergonomics boost, but is no longer suitable for exchange",
                    ShortName = $"{RaritySettings.GetByName(quality).ShortName} C.(L)",
                    ItemTplToClone = "58d2912286f7744e27117493",
                    HandbookPriceRoubles = modConfig.VariantCores.Price[quality] * modConfig.VariantCores.LockedCoresPrice * 2,
                    Rarity = quality,
                    VariantType = quality
                };
                if (enabled && modConfig.VariantCoresEnabled)
                {
                    variant.Barter = modConfig.VariantCores.LockedCoresBarter;
                    variant.Barter.BarterPrice = [];
                    variant.Barter.BarterPrice.Add(idDatabaseManager.GetCustomId($"{quality} Quality Variant Core:ID"), modConfig.VariantCores.LockedCoresPrice);
                }
                var newId = GenerateItem(
                    $"{quality} Quality Variant Core (Locked)",
                    variant,
                    "<color={rarity.Color}><b>{quality} Quality Variant Core (Locked)</b></color>",
                    $"An upgraded core component for Variant Weapons. Provides higher ergonomics than the standard version and can be inserted into a compatible slot in a weapon variant of the same quality\nCannot be used to purchase Variant Blind Boxes, but will not be consumed when buying them while installed in a weapon",
                    variantCorePropertiesOverride
                );
                if (newId is null) { continue; }
                if (customSlotsChanger.coreMods.TryGetValue(quality, out var coreMods))
                {
                    coreMods.Add(newId);
                }
                else { customSlotsChanger.coreMods[quality] = [newId]; }
            }
            // Generate Variant Weapon Blind Boxes
            foreach (var (quality, enabled) in modConfig.Generate)
            {
                var variant = new VariantConfiguration
                {
                    Description = $"Open to receive one random Weapon Variant of <b>{quality}</b> Quality",
                    ShortName = $"{RaritySettings.GetByName(quality).ShortName} Weapon Blind Box",
                    ItemTplToClone = "6489b2b131a2135f0d7d0fcb",
                    HandbookPriceRoubles = modConfig.BlindBoxes.Price[quality],
                    Rarity = quality,
                    VariantType = quality
                };
                var coreId = customSlotsChanger.GetItemFromString($"{quality} Quality Variant Core")?.Id ?? "5449016a4bdc2d6f028b456f";
                if (modConfig.BlindBoxes.Price[quality] > 0 && enabled && modConfig.VariantCoresEnabled)
                {
                    var barter = modConfig.BlindBoxesEnabled ? cloner.Clone(modConfig.BlindBoxes.BoxesBarter) ?? new CustomBarterConfig() : new CustomBarterConfig();
                    if (modConfig.VariantCoresEnabled && modConfig.BlindBoxes.CoresPrice > 0)
                        barter.BarterPrice.Add(coreId, modConfig.BlindBoxes.CoresPrice);
                    else
                        barter.BarterPrice.Add("5449016a4bdc2d6f028b456f", modConfig.BlindBoxes.Price[quality]);
                    variant.Barter = barter;
                }
                GenerateItem(
                    $"{quality} Quality Variant Weapon Blind Box",
                    variant,
                    "Contains one weapon of <color={rarity.Color}><b>{quality} Quality Variant</b></color>",
                    $"This item can be opened to receive one random Weapon Variant of <b>{quality}</b> Quality",
                    new TemplateItemProperties
                    {
                        Width = modConfig.BlindBoxes.Width,
                        Height = modConfig.BlindBoxes.Height
                    }
                );
            }
            
            // Create Universal Variant Weapon Cores
            variantCorePropertiesOverride.Ergonomics = modConfig.VariantCores.LockedCoresErgonomics;
            for (int i = 0; i < 5; i++)
            {
                var universalCorePropertiesOverride = cloner.Clone(variantCorePropertiesOverride);
                if (universalCorePropertiesOverride is null) { continue; }

                universalCorePropertiesOverride.Ergonomics += (i);
                universalCorePropertiesOverride.Recoil -= (5-i);

                var newId = GenerateItem(
                    $"Universal Variant Weapon Core v1.{i}",
                    new VariantConfiguration
                    {
                        Description = "A rare, highly adaptable chip used in Variant Weapons. Unlike other variant cores, it can interface with any variant regardless of quality",
                        ShortName = $"Univ C.v1.{i}",
                        ItemTplToClone = "58d2912286f7744e27117493",
                        HandbookPriceRoubles = modConfig.VariantCores.Price["Unique"] * 5,
                        Rarity = "Unknown",
                        VariantType = "Unknown"
                    },
                    "<color={rarity.Color}><b>Special attachment for Mod Core slot only available on Weapon Variants</b></color>",
                    "A rare core component compatible with all Variant Weapons, regardless of quality. Provides increased ergonomics and reduces recoil when installed\nOnly obtainable from Unknown Variant Weapon Core Packages",
                    universalCorePropertiesOverride
                );
                if (newId is null) { continue; }
                foreach (var (_, coreMod) in customSlotsChanger.coreMods) coreMod.Add(newId);
                RarityData rarity = RaritySettings.GetByName("Unknown");
                unknownPackageLootpool.Add($"<color={rarity.Color}><b>Universal Variant Weapon Core v1.{i} (Ergonomics +{universalCorePropertiesOverride.Ergonomics}, Recoil {universalCorePropertiesOverride.Recoil}%)</b></color>", 1);
            }

            // Create Unknown Variant Weapon Core Package
            var unknownPackageOverride = cloner.Clone(variantCorePropertiesOverride);
            if (unknownPackageOverride is not null)
            {
                unknownPackageOverride.Ergonomics = 0;
                unknownPackageOverride.Width = 2;
                unknownPackageOverride.Height = 1;
                unknownPackageOverride.StackMaxSize = 1;

                var totalWeight = unknownPackageLootpool.Values.Sum();
                var formatted = unknownPackageLootpool
                    .Select(kvp => new
                    {
                        Name = kvp.Key,
                        Percentage = (double)kvp.Value / totalWeight * 100
                    })
                    .OrderBy(x => x.Percentage);
                var descText = "";
                foreach (var item in formatted)
                {
                    descText += $"{item.Percentage:F2}% - {item.Name}\n";
                }

                GenerateItem(
                    $"Unknown Variant Weapon Core Package",
                    new VariantConfiguration
                    {
                        Description = "A sealed bundle of Variant Weapon Cores of unknown quality. Only one is visible from the outside-open it to reveal the rest",
                        ShortName = $"{RaritySettings.GetByName("Unknown").ShortName} Package",
                        ItemTplToClone = "6489b2b131a2135f0d7d0fcb",
                        HandbookPriceRoubles = modConfig.VariantCores.Price["Meme"] * 6,
                        Barter = new CustomBarterConfig
                        {
                            BarterPrice = { ["MONEY_ROUBLES"] = modConfig.VariantCores.UnknownPackagePriceInRoubles },
                            TraderId = "SKIER",
                            LoyalLevel = 1,
                            UnlimitedCount = true,
                            StackObjectsCount = 99

                        },
                        Rarity = "Unknown",
                        VariantType = "Unknown"
                    },
                    "<color={rarity.Color}><b>Package of 5 random Variant Weapon Cores</b></color>",
                    $"Open it to receive 5 random Variant Weapon Cores\nThis is the only way to obtain rare Universal Variant Weapon Cores\n\n>>> Lootpool <<<\n{descText}\nCan be found in Jackets/Dead scavs/PC Blocks/Plastic Suitcases and Safes on all maps",
                    unknownPackageOverride
                );
            }
            // Generate all items from jsons
            foreach (var (variantName, config) in modDatabaseLoader.DbItems)
            {
                GenerateItem(
                    variantName,
                    config,
                    "Part of <color={rarity.Color}><b>{variant.VariantType} Variant</b></color>",
                    "This item is designed for weapons from the Definitive Weapon Variants mod and cannot be used without a compatible variant weapon",
                    new TemplateItemProperties()
                );
            }
        }




        public string? GenerateItem(string variantName, VariantConfiguration config, string additionalDescription, string explanation, TemplateItemProperties newOverride)
        {
            
            if (config is { Description: not null, ShortName: not null, ItemTplToClone: not null, Rarity: not null, HandbookPriceRoubles: not null, VariantType: not null } variant)
            {
                if (!MongoId.IsValidMongoId(variant.ItemTplToClone)) {
                    logger.LogWithColor($"[{GetType().Namespace}] ItemTplToClone {variant.ItemTplToClone} is incorrect ({variantName})!", LogTextColor.Red);
                    return null;
                }
                MongoId itemTplToClone = (MongoId)variant.ItemTplToClone;
                items.TryGetValue(itemTplToClone, out var copiedItem);
                if (copiedItem is null)
                {
                    logger.LogWithColor($"[{GetType().Namespace}] ItemTplToClone {variant.ItemTplToClone} is not found (or you are missing some mod) ({variantName})! Skipping", LogTextColor.Yellow);
                    return null;
                }

                HandbookItem? copiedItemHandbook = handbook.Items.Find(t => t.Id == itemTplToClone);
                RarityData rarity = RaritySettings.GetByName(variant.Rarity);
                if (variant.Barter is not null && modConfig.AmonyaTraderMode) variant.Barter.TraderId = "ee840a5ba014e9c5478d5ccd";
                var traderName = (variant.Barter == null || customItemCreator.GetTraderIdByName(variant.Barter.TraderId) == null) ? "N/A" : traders[(MongoId)customItemCreator.GetTraderIdByName(variant.Barter.TraderId)!].Base.Nickname;
                var text = variant.Barter == null ? "Can't be bought from traders" : $"Can be bought in {traderName} LL{variant.Barter.LoyalLevel}";
                var newItem = new NewItemFromCloneDetails
                {
                    ItemTplToClone = itemTplToClone,
                    ParentId = variant.Changes?.Parent != null ? variant.Changes.Parent : copiedItem.Parent,
                    HandbookParentId = copiedItemHandbook != null ? copiedItemHandbook.ParentId : "5b5f6fa186f77409407a7eb7",
                    NewId = idDatabaseManager.GetCustomId($"{variantName}:ID"),
                    FleaPriceRoubles = variant.HandbookPriceRoubles * 2,
                    HandbookPriceRoubles = variant.HandbookPriceRoubles,
                    OverrideProperties = newOverride,
                    Locales = new Dictionary<string, LocaleDetails>
                    {
                        {
                            "en", new LocaleDetails
                            {
                                Name = GenerateVariantName(variant.Rarity, rarity, variantName), 
                                ShortName = variant.ShortName,
                                Description = string.Join("\n", new[] {
                                    $"<align=\"center\">{variant.Description}",
                                    $"",
                                    additionalDescription.Replace("{rarity.Color}", rarity.Color).Replace("{variant.VariantType}", variant.VariantType).Replace("{quality}", variant.VariantType),
                                    $"",
                                    explanation,
                                    $"{text}</align>"
                                })
                            }
                        }
                    }
                };
                newItem.OverrideProperties.BackgroundColor = IsPluginLoaded() ? $"{rarity.Color}ff" : rarity.BgColor;

                if (variant.Properties != null)
                    newItem.OverrideProperties = customPropertiesChanger.ChangeItemProperties(variant.Properties, newItem.OverrideProperties, copiedItem, config, variantName);

                if (variant?.Changes?.Cartridges != null)
                {
                    var newCartridges = customSlotsChanger.CartridgesChanger(variant.Changes.Cartridges, copiedItem, newItem, variantName);
                    if (newCartridges != null) newItem.OverrideProperties.Cartridges = newCartridges;

                    // Revolver slot changes when changing cartidges filter!
                    if (copiedItem.Properties?.Slots?.Count() > 0)
                    {
                        foreach (var slot in copiedItem.Properties.Slots)
                        {
                            if (slot?.Name is not null && slot.Name.Contains("camora_"))
                            {
                                if (variant.Changes.Slots is null) variant.Changes.Slots = [];
                                variant.Changes.Slots.Add(slot.Name, variant.Changes.Cartridges);
                            }
                        }
                    }
                }
                if (variant?.Changes?.Chambers != null)
                {
                    var newChambers = customSlotsChanger.ChambersChanger(
                        variant?.Changes?.Chambers,
                        copiedItem,
                        newItem,
                        variantName
                    );
                    if (newChambers != null)
                    {
                        newItem.OverrideProperties.Chambers = newChambers;
                        var newChamberFilter = newChambers?.First()?.Properties?.Filters?.First().Filter;
                        if (newChamberFilter?.Count > 0)
                        {
                            var firstId = newChamberFilter.First();
                            newItem.OverrideProperties.DefAmmo = firstId;

                            if (items.TryGetValue(firstId, out var item) && item?.Properties?.Caliber != null)
                            {
                                newItem.OverrideProperties.AmmoCaliber = item.Properties.Caliber;
                            }
                            else
                            {
                                logger.LogWithColor($"[{GetType().Namespace}] Ammo for {variantName} is incorrect: {firstId}", LogTextColor.Red);
                            }
                        }
                        else
                        {
                            logger.LogWithColor($"[{GetType().Namespace}] Item '{variantName}' don't have any ammunition allowed in chambers!", LogTextColor.Red);
                        }
                    }
                    else
                    {
                        logger.LogWithColor($"[{GetType().Namespace}] Item '{variantName}' failed to generate new chambers", LogTextColor.Red);
                    }
                }
                if (variant?.Changes?.Slots != null)
                {
                    var newSlots = customSlotsChanger.SlotsChanger(variant.Changes.Slots, copiedItem, newItem);
                    if (newSlots != null) newItem.OverrideProperties.Slots = newSlots;
                }

                if (variant?.Barter is not null)
                {
                    foreach (var (barter, price) in variant.Barter.BarterPrice.ToList())
                    {
                        var barterId = customSlotsChanger.GetItemFromString(barter)?.Id;
                        if (barterId is null) continue;
                        variant.Barter.BarterPrice.Remove(barter);
                        variant.Barter.BarterPrice[barterId] = price;
                    }
                }
                customItemCreator.AddItemToDatabase(newItem, new CustomItemConfig(), variant?.Barter ?? new CustomBarterConfig());
                return newItem.NewId;
            } else
            {
                logger.LogWithColor($"[{GetType().Namespace}] Item '{variantName}' is missing one or more required properties!", LogTextColor.Red);
                return null;
            }
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
        private static string GenerateVariantName(string? rarityName, RarityData rarity, string variantName)
        {
            if (rarityName == "Unique")
            {
                return $"<b>{RainbowText.RainbowUnityRichText(variantName)}</b>";
            }
            return $"<b><color={rarity.Color}>{variantName}</color></b>";
        }
    }
}
