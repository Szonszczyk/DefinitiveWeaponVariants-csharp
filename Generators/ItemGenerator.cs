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

        public void GenerateItems()
        {
            foreach (var (variantName, config) in modDatabaseLoader.DbItems)
            {
                if (config is { Description: not null, ShortName: not null, ItemTplToClone: not null, Rarity: not null, HandbookPriceRoubles: not null, VariantType: not null } variant)
                {
                    if (!MongoId.IsValidMongoId(variant.ItemTplToClone)) {
                        logger.LogWithColor($"[{GetType().Namespace}] ItemTplToClone {variant.ItemTplToClone} is incorrect ({variantName})!", LogTextColor.Red);
                        continue;
                    }
                    MongoId itemTplToClone = (MongoId)variant.ItemTplToClone;
                    items.TryGetValue(itemTplToClone, out var copiedItem);
                    if (copiedItem is null)
                    {
                        logger.LogWithColor($"[{GetType().Namespace}] ItemTplToClone {variant.ItemTplToClone} is not found (or you are missing some mod) ({variantName})! Skipping", LogTextColor.Yellow);
                        continue;
                    }

                    HandbookItem? copiedItemHandbook = handbook.Items.Find(t => t.Id == itemTplToClone);
                    if (copiedItemHandbook == null) {
                        logger.LogWithColor($"[{GetType().Namespace}] Item {itemTplToClone} handbook entry is missing ({variantName})! Skipping", LogTextColor.Yellow);
                        continue;
                    }
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
                        OverrideProperties = new TemplateItemProperties
                        {
                            BackgroundColor = IsPluginLoaded() ? $"{rarity.Color}ff" : rarity.BgColor
                        },
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
                                        $"Part of <color={rarity.Color}><b>{variant.VariantType} Variant</b></color>",
                                        $"",
                                        $"This item is integral part of weapon from Definitive Weapon Variants mod and can't be used without proper variant weapon.",
                                        $"{text}</align>"
                                    })
                                }
                            }
                        }
                    };

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
                } else
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Item '{variantName}' is missing one or more required properties!", LogTextColor.Red);
                }
            }
        }

        public void GenerateVariantCores()
        {
            foreach (var (quality, enabled) in modConfig.Generate)
            {
                var variantName = $"{quality} Quality Variant Core";
                var variant = new VariantConfiguration
                {
                    ShortName = $"{quality} C.",
                    ItemTplToClone = "58d2912286f7744e27117493",
                    HandbookPriceRoubles = modConfig.VariantCores.Price[quality],
                    Rarity = quality
                };
                if (enabled && modConfig.VariantCores.Buyable)
                {
                    var barter = modConfig.VariantCores.CoresBarter;
                    barter.BarterPrice = [];
                    barter.BarterPrice.Add("5449016a4bdc2d6f028b456f", modConfig.VariantCores.Price[quality]);
                    variant.Barter = barter;
                }
                MongoId itemTplToClone = (MongoId)variant.ItemTplToClone;
                TemplateItem copiedItem = items[itemTplToClone];
                HandbookItem? copiedItemHandbook = handbook.Items.Find(t => t.Id == itemTplToClone);

                RarityData rarity = RaritySettings.GetByName(variant.Rarity);
                if (variant.Barter is not null && modConfig.AmonyaTraderMode) variant.Barter.TraderId = "ee840a5ba014e9c5478d5ccd";
                var traderName = (variant.Barter == null || customItemCreator.GetTraderIdByName(variant.Barter.TraderId) == null) ? "N/A" : traders[(MongoId)customItemCreator.GetTraderIdByName(variant.Barter.TraderId)!].Base.Nickname;
                var newItem = new NewItemFromCloneDetails
                {
                    ItemTplToClone = itemTplToClone,
                    ParentId = variant.Changes?.Parent != null ? variant.Changes.Parent : copiedItem.Parent,
                    HandbookParentId = copiedItemHandbook != null ? copiedItemHandbook.ParentId : "5b5f6fa186f77409407a7eb7",
                    NewId = idDatabaseManager.GetCustomId($"{variantName}:ID"),
                    FleaPriceRoubles = variant.HandbookPriceRoubles * 2,
                    HandbookPriceRoubles = variant.HandbookPriceRoubles,
                    OverrideProperties = new TemplateItemProperties
                    {
                        BackgroundColor = IsPluginLoaded() ? $"{rarity.Color}ff" : rarity.BgColor,
                        StackMaxSize = 10,
                        Prefab = new Prefab{
                            Path = "assets/content/items/barter/cpu/item_cpu.bundle",
                            Rcid = ""
                        },
                        Ergonomics = 5,
                        ExtraSizeDown = 0,
                        ToolModdable = true,
                        RaidModdable = true,
                        Recoil = 0,
                        Slots = []
                    },
                    Locales = new Dictionary<string, LocaleDetails>
                    {
                        {
                            "en", new LocaleDetails
                            {
                                Name = GenerateVariantName(variant.Rarity, rarity, variantName),
                                ShortName = variant.ShortName,
                                Description = string.Join("\n", new[] {
                                    $"<align=\"center\">This small and flat chip is a core item that is making variant weapon - The Variant Weapon",
                                    $"",
                                    $"Special currency <color={rarity.Color}><b>{quality} Quality Variant Core</b></color> ",
                                    $"",
                                    $"This item is special currency used to buy Variant Blind Boxes, but can also be inserted into special slot in weapon variant of the same quality.{(modConfig.VariantCores.FoundOnEnemies.TryGetValue("assault", out _) ? $"\nCan be found in Scav pockets" : "")}",
                                    $"{(variant.Barter == null ? "Can't be bought from traders" : $"Can be bought in {traderName} LL{variant.Barter.LoyalLevel}")}</align>"
                                })
                            }
                        }
                    }
                };
                customItemCreator.AddItemToDatabase(newItem, new CustomItemConfig(), variant.Barter ?? new CustomBarterConfig());
                
            }
        }

        public void GenerateBlindBoxes()
        {
            foreach (var (quality, enabled) in modConfig.Generate)
            {
                var variantName = $"{quality} Quality Variant Weapon Blind Box";
                var variant = new VariantConfiguration
                {
                    ShortName = $"{quality} Weapon Blind Box",
                    ItemTplToClone = "6489b2b131a2135f0d7d0fcb",
                    HandbookPriceRoubles = modConfig.BlindBoxes.Price[quality],
                    Rarity = quality
                };
                var coreId = customSlotsChanger.GetItemFromString($"{quality} Quality Variant Core")?.Id ?? "5449016a4bdc2d6f028b456f";
                if (modConfig.BlindBoxes.Price[quality] > 0 && enabled)
                {
                    var barter = modConfig.BlindBoxesEnabled ? cloner.Clone(modConfig.BlindBoxes.BoxesBarter) ?? new CustomBarterConfig() : new CustomBarterConfig();
                    if (modConfig.VariantCoresEnabled && modConfig.BlindBoxes.CoresPrice > 0)
                        barter.BarterPrice.Add(coreId, modConfig.BlindBoxes.CoresPrice);
                    else
                        barter.BarterPrice.Add("5449016a4bdc2d6f028b456f", modConfig.BlindBoxes.Price[quality]);
                    variant.Barter = barter;
                }
                MongoId itemTplToClone = (MongoId)variant.ItemTplToClone;
                TemplateItem copiedItem = items[itemTplToClone];
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
                    OverrideProperties = new TemplateItemProperties
                    {
                        BackgroundColor = IsPluginLoaded() ? $"{rarity.Color}ff" : rarity.BgColor,
                        Width = modConfig.BlindBoxes.Width,
                        Height = modConfig.BlindBoxes.Height
                    },
                    Locales = new Dictionary<string, LocaleDetails>
                    {
                        {
                            "en", new LocaleDetails
                            {
                                Name = GenerateVariantName(variant.Rarity, rarity, variantName),
                                ShortName = variant.ShortName,
                                Description = string.Join("\n", new[] {
                                    $"<align=\"center\">Open to receive one of {quality} quality weapon variants",
                                    $"",
                                    $"Contains one weapon of <color={rarity.Color}><b>{quality} Quality Variant</b></color>",
                                    $"",
                                    $"This item can be opened to receive one of {quality} quality weapon variants",
                                    $"{text}</align>"
                                })
                            }
                        }
                    }
                };
                customItemCreator.AddItemToDatabase(newItem, new CustomItemConfig(), variant.Barter ?? new CustomBarterConfig());
                items[newItem.NewId].Name = newItem.NewId;
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
