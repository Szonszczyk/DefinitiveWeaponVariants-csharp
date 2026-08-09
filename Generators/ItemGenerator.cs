using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace DefinitiveWeaponVariants.Generators;

[Injectable(InjectionType.Singleton)]
public class ItemGenerator(
    CustomLogger logger,
    ModDatabaseLoader modDatabaseLoader,
    IdDatabaseManager idDatabaseManager,
    CustomItemCreator customItemCreator,
    CustomPropertiesChanger customPropertiesChanger,
    CustomSlotsChanger customSlotsChanger,
    ConfigData config,
    ModDataStorage modDataStorage
)
{
    private readonly ConfigData modConfig = config;

    public void GenerateAllItems()
    {
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
                logger.Error($"ItemTplToClone {variant.ItemTplToClone} is incorrect ({variantName})!");
                return null;
            }
            MongoId itemTplToClone = (MongoId)variant.ItemTplToClone;
            modDataStorage.Items.TryGetValue(itemTplToClone, out var copiedItem);
            if (copiedItem is null)
            {
                logger.Warning($"ItemTplToClone {variant.ItemTplToClone} is not found (or you are missing some mod) ({variantName})! Skipping");
                return null;
            }

            HandbookItem? copiedItemHandbook = modDataStorage.Handbook.Items.Find(t => t.Id == itemTplToClone);
            RarityData rarity = RaritySettings.GetByName(variant.Rarity);
            if (variant.Barter is not null && modConfig.AmonyaTraderMode) variant.Barter.TraderId = "ee840a5ba014e9c5478d5ccd";
            var traderName = (variant.Barter == null || customItemCreator.GetTraderIdByName(variant.Barter.TraderId) == null) ? "N/A" : modDataStorage.Traders[(MongoId)customItemCreator.GetTraderIdByName(variant.Barter.TraderId)!].Base.Nickname;
            var text = variant.Barter == null ? "Can't be bought from traders" : $"Can be bought in {traderName} LL{variant.Barter.LoyalLevel}";
            var newItem = new NewItemFromCloneDetails
            {
                NewItemName = variantName,
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
            newItem.OverrideProperties.BackgroundColor = ModDataStorage.IsPluginLoaded() ? $"{rarity.Color}ff" : rarity.BgColor;

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

                        if (modDataStorage.Items.TryGetValue(firstId, out var item) && item?.Properties?.Caliber != null)
                        {
                            newItem.OverrideProperties.AmmoCaliber = item.Properties.Caliber;
                        }
                        else
                        {
                            logger.Error($"Ammo for {variantName} is incorrect: {firstId}");
                        }
                    }
                    else
                    {
                        logger.Error($"Item '{variantName}' don't have any ammunition allowed in chambers!");
                    }
                }
                else
                {
                    logger.Error($"Item '{variantName}' failed to generate new chambers");
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
            modDataStorage.AddItemToQuality(newItem.NewId, variant.Rarity);
            return newItem.NewId;
        } else
        {
            logger.Error($"Item '{variantName}' is missing one or more required properties!");
            return null;
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
