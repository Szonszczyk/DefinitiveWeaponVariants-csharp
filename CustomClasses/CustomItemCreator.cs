using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Interfaces;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;

namespace DefinitiveWeaponVariants.CustomClasses;

[Injectable(InjectionType.Singleton)]
public class CustomItemCreator(
    CustomLogger logger,
    CustomItemService customItemService,
    ICloner cloner,
    ModDataStorage modDataStorage,
    ItemHelper itemHelper
)
{
    public Dictionary<MongoId, TemplateItem> ItemsAdded { get; set; } = [];

    public void AddItemToDatabase(NewItemFromCloneDetails item, CustomItemConfig itemConfig, CustomBarterConfig barterConfig)
    {
        if (item.NewId == null) return;

        var newItem = customItemService.CreateItemFromClone(item);

        if (newItem?.ItemId == null) return;

        modDataStorage.Items.TryGetValue((MongoId)newItem.ItemId, out var newItemTemplate);

        if (newItemTemplate is not null)
            ItemsAdded.Add((MongoId)newItem.ItemId, newItemTemplate);

        if (itemConfig.AirdropBlacklisted && modDataStorage.ConfigServerAirdropConfig is not null)
        {
            foreach (var (_, airdrop) in modDataStorage.ConfigServerAirdropConfig.Loot)
            {
                airdrop.ItemBlacklist.Add(item.NewId);
            }
        }
        if (itemConfig.FenceBlacklisted && modDataStorage.ConfigServerTraderConfig is not null)
        {
            modDataStorage.ConfigServerTraderConfig.Fence.Blacklist.Add(item.NewId);
        }
        if (itemConfig.FleaBlacklisted && modDataStorage.ConfigServerRagfairConfig is not null)
        {
            modDataStorage.ConfigServerRagfairConfig.Dynamic.Blacklist.Custom.Add(item.NewId);
        }
        if (itemConfig.AddToInventorySlots.Count > 0)
        {
            AddItemToInventorySlots(item.NewId, itemConfig);
        }
        if (itemConfig.MasteryName != "")
        {
            AddItemToMasteries(item.NewId, itemConfig);
        }
        if (itemConfig.Presets.Count > 0 && modDataStorage.GlobalsData is not null)
        {
            foreach (var (presetId, preset) in itemConfig.Presets)
            {
                modDataStorage.GlobalsData.ItemPresets[presetId] = preset;
            }
        }
        if (barterConfig.LoyalLevel != 0 && barterConfig.BarterPrice.Count > 0)
        {
            AddItemToTrader(item.NewId, barterConfig);
        }
    }
    private void AddItemToInventorySlots(string itemId, CustomItemConfig itemConfig)
    {
        TemplateItem defaultInventory = modDataStorage.Items["55d7217a4bdc2d86028b456d"];
        if (defaultInventory.Properties == null) return;
        IEnumerable<Slot>? defaultInventorySlots = defaultInventory.Properties.Slots;
        if (defaultInventorySlots != null && defaultInventorySlots.Any())
        {
            foreach (var slot in defaultInventorySlots)
            {
                if (slot.Name == null) continue;

                if (itemConfig.AddToInventorySlots.Contains(slot.Name) && slot.Properties != null)
                {
                    var filters = slot.Properties.Filters;
                    if (filters != null)
                    {
                        foreach (var filter in filters)
                        {
                            if (filter != null && filter.Filter != null && !filter.Filter.Contains(itemId))
                            {
                                filter.Filter.Add(itemId);
                            }
                        }
                    }
                    
                }
            }
        }
    }
    private void AddItemToMasteries(string itemId, CustomItemConfig itemConfig)
    {
        var mastering = modDataStorage.GlobalsData.Configuration.Mastering;
        var existingMastery = mastering.FirstOrDefault(existing => existing.Name == itemConfig.MasteryName);
        if (existingMastery != null)
        {
            existingMastery.Templates = existingMastery.Templates.Append(itemId);
        }
        else
        {
            logger.Error($"MasteryName '{itemConfig.MasteryName}' is incorrect!");
        }
    }
    public void AddItemToTrader(string itemId, CustomBarterConfig barterConfig)
    {
        var traderId = GetTraderIdByName(barterConfig.TraderId);
        if (traderId == null)
        {
            logger.Error($"Trader name / Trader ID '{traderId}' is incorrect!");
            return;
        }
        var trader = modDataStorage.Traders[(MongoId)traderId];

        foreach (var (addBarterId, _) in barterConfig.BarterPrice)
        {
            var addBarter = GetItemIdByName(addBarterId);
            if (addBarter == null)
            {
                logger.Error($"Barter item of id '{addBarterId}' is incorrect! Item {itemId} was not added to trader");
                return;
            }
        }

        var newItem = new Item
        {
            Id = itemId,
            Template = itemId,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                UnlimitedCount = barterConfig.UnlimitedCount,
                StackObjectsCount = barterConfig.StackObjectsCount
            }
        };
        var assort = trader.Assort.Items;
        assort?.Add(newItem);

        List<BarterScheme> newBarterSchemes = [];

        foreach (var (addBarterId, price) in barterConfig.BarterPrice)
        {
            var id = GetItemIdByName(addBarterId)!;
            var newBarterScheme = new BarterScheme
            {
                Count = price,
                Template = (MongoId)id
            };
            newBarterSchemes.Add(newBarterScheme);

        }
        var assortBarterScheme = trader.Assort.BarterScheme;
        if (!assortBarterScheme.ContainsKey(itemId))
        {
            assortBarterScheme[itemId] = [];
            assortBarterScheme[itemId].Add(newBarterSchemes);
        }
        trader.Assort.LoyalLevelItems[itemId] = barterConfig.LoyalLevel;
    }

    public void CreateHideoutCraft(MongoId id, string craftIdToCopy, Dictionary<string, int> requiredItems, int productionTime, string newCraftId)
    {
        var recipes = modDataStorage.HideoutData?.Production?.Recipes;
        if (recipes == null)
        {
            logger.Error("Cannot add craft: Recipes collection is null.");
            return;
        }
        var sourceRecipe = recipes.FirstOrDefault(e => e.Id == craftIdToCopy);
        if (sourceRecipe == null)
        {
            logger.Error($"Can't find craft of id: {craftIdToCopy}");
            return;
        }
        var newRecipe = cloner.Clone(sourceRecipe);
        if (newRecipe is null) { return; }
        newRecipe.Requirements = sourceRecipe.Requirements?.Take(1).ToList() ?? [];
        foreach (var (requiredItemId, count) in requiredItems)
        {
            newRecipe.Requirements.Add(new SPTarkov.Server.Core.Models.Eft.Hideout.Requirement
            {
                TemplateId = requiredItemId,
                Count = count,
                IsEncoded = false,
                IsFunctional = false,
                IsSpawnedInSession = false,
                Type = "Item"
            });
        }
        newRecipe.Id = newCraftId;
        newRecipe.EndProduct = id;
        newRecipe.Count = 1;
        newRecipe.ProductionTime = productionTime;
        recipes.Add(newRecipe);
    }

    public void CreateCultistCircleCraft(List<string> reward, List<string> requiredItems, int craftTimeSeconds, bool repeatable)
    {
        var newdirectReward = new DirectRewardSettings
        {
            Reward = reward
                    .Select(i => new MongoId(i))
                    .ToList(),
            RequiredItems = requiredItems
                    .Select(i => new MongoId(i))
                    .ToList(),

            CraftTimeSeconds = craftTimeSeconds,
            Repeatable = repeatable
        };
        modDataStorage.HideoutConfigData.CultistCircle.DirectRewards.Add(newdirectReward);
    }

    public void AddItemToSecureContainer(string id)
    {
        var allContainers = itemHelper.GetItemTplsOfBaseType("5448bf274bdc2dfc2f8b456a");
        foreach (var containerId in allContainers)
        {
            modDataStorage.Items.TryGetValue(containerId, out var container);
            if (container is null || container?.Properties?.Grids is null) { continue; }
            foreach(var grid in container.Properties.Grids)
            {
                if (grid?.Properties?.Filters is not null && grid.Properties.Filters.Any())
                {
                    grid.Properties.Filters.FirstOrDefault().Filter.Add(id);
                }
                
            }
        }
    }

    public MongoId? GetTraderIdByName(string name)
    {
        var field = typeof(Traders).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field != null && field.GetValue(null) is MongoId id)
        {
            return id;
        }
        if (!MongoId.IsValidMongoId(name) || !modDataStorage.Traders.TryGetValue(name, out _)) return null;

        return name;
    }

    public MongoId? GetItemIdByName(string name)
    {
        var field = typeof(ItemTpl).GetField(name, BindingFlags.Public | BindingFlags.Static);
        if (field != null && field.GetValue(null) is MongoId id)
        {
            return id;
        }
        if (!MongoId.IsValidMongoId(name) || !modDataStorage.Items.TryGetValue(name, out _)) return null;

        return name;
    }
}
