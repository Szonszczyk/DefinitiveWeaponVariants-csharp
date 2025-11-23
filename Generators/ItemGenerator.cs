using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace DefinitiveWeaponVariants.Generators
{
    internal class ItemGenerator(
        ISptLogger<DefinitiveWeaponVariants> logger,
        DatabaseService databaseService,
        ModDatabaseLoader modDatabaseLoader,
        IdDatabaseManager idDatabaseManager,
        CustomItemCreator customItemCreator,
        CustomPropertiesChanger customPropertiesChanger,
        CustomSlotsChanger customSlotsChanger
    )
    {
        private readonly Dictionary<MongoId, TemplateItem> items = databaseService.GetItems();
        private readonly HandbookBase handbook = databaseService.GetHandbook();
        private readonly Dictionary<MongoId, Trader> traders = databaseService.GetTraders();
       
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
                    TemplateItem copiedItem = items[itemTplToClone];
                    HandbookItem? copiedItemHandbook = handbook.Items.Find(t => t.Id == itemTplToClone);
                    if (copiedItemHandbook == null) {
                        logger.LogWithColor($"[{GetType().Namespace}] Item {itemTplToClone} handbook entry is missing ({variantName})!", LogTextColor.Red);
                        continue;
                    }
                    RarityData rarity = RaritySettings.GetByName(variant.Rarity);
                    var traderName = (variant.Barter == null || customItemCreator.GetTraderIdByName(variant.Barter.TraderId) == null) ? "N/A" : traders[(MongoId)customItemCreator.GetTraderIdByName(variant.Barter.TraderId)!].Base.Nickname;
                    var text = variant.Barter == null ? "Can't be bought from traders" : $"Can be bought in {traderName} LL{variant.Barter.LoyalLevel}";
                    var newItem = new NewItemFromCloneDetails
                    {
                        ItemTplToClone = itemTplToClone,
                        ParentId = variant.Changes?.Parent != null ? variant.Changes.Parent : copiedItem.Parent,
                        HandbookParentId = copiedItemHandbook.ParentId,
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
                        var newSlots = customSlotsChanger.SlotsChanger(variant.Changes.Slots, copiedItem, newItem, variantName);
                        if (newSlots != null) newItem.OverrideProperties.Slots = newSlots;
                    }
                    
                    
                    customItemCreator.AddItemToDatabase(newItem, new CustomItemConfig(), config.Barter ?? new CustomBarterConfig());
                }
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
