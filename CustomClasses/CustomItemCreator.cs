using DefinitiveWeaponVariants.Interfaces;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;

namespace DefinitiveWeaponVariants.CustomClasses
{
    
    public class CustomItemCreator(
        ISptLogger<DefinitiveWeaponVariants> logger,
        ConfigServer configServer,
        CustomItemService customItemService,
        DatabaseService databaseService,
        ICloner cloner
    )
    {
        private readonly Globals globals = databaseService.GetGlobals();
        private readonly Dictionary<MongoId, TemplateItem> items = databaseService.GetItems();
        private readonly Dictionary<MongoId, Trader> traders = databaseService.GetTraders();
        private readonly SPTarkov.Server.Core.Models.Spt.Hideout.Hideout hideoutCrafts = databaseService.GetHideout();
        public int itemsLoaded = 0;

        public void AddItemToDatabase(NewItemFromCloneDetails item, CustomItemConfig itemConfig, CustomBarterConfig barterConfig)
        {
            if (item.NewId == null) return;

            customItemService.CreateItemFromClone(item);

            if (itemConfig.AirdropBlacklisted)
            {
                var airdropConfig = configServer.GetConfig<AirdropConfig>();
                foreach (var airdrop in airdropConfig.Loot)
                {
                    airdropConfig.Loot[airdrop.Key].ItemBlacklist.Add(item.NewId);
                }
            }
            if (itemConfig.FenceBlacklisted)
            {
                TraderConfig traderConfig = configServer.GetConfig<TraderConfig>();
                traderConfig.Fence.Blacklist.Add(item.NewId);
            }
            if (itemConfig.FleaBlacklisted)
            {
                var fleaConfig = configServer.GetConfig<RagfairConfig>();
                fleaConfig.Dynamic.Blacklist.Custom.Add(item.NewId);
            }
            if (itemConfig.AddToInventorySlots.Count > 0)
            {
                AddItemToInventorySlots(item.NewId, itemConfig);
            }
            if (itemConfig.MasteryName != "")
            {
                AddItemToMasteries(item.NewId, itemConfig);
            }
            if (itemConfig.Presets.Count > 0)
            {
                foreach (var (presetId, preset) in itemConfig.Presets)
                {
                    globals.ItemPresets[presetId] = preset;
                }
            }
            if (barterConfig.LoyalLevel != 0)
            {
                AddItemToTrader(item.NewId, barterConfig);
            }

            itemsLoaded++;
        }
        private void AddItemToInventorySlots(string itemId, CustomItemConfig itemConfig)
        {
            TemplateItem defaultInventory = items["55d7217a4bdc2d86028b456d"];
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
            var mastering = globals.Configuration.Mastering;
            var existingMastery = mastering.FirstOrDefault(existing => existing.Name == itemConfig.MasteryName);
            if (existingMastery != null)
            {
                existingMastery.Templates = existingMastery.Templates.Append(itemId);
            }
            else
            {
                logger.LogWithColor($"[{GetType().Namespace}] MasteryName '{itemConfig.MasteryName}' is incorrect!", LogTextColor.Red);
            }
        }
        public void AddItemToTrader(string itemId, CustomBarterConfig barterConfig)
        {
            var traderId = GetTraderIdByName(barterConfig.TraderId);
            if (traderId == null)
            {
                logger.LogWithColor($"[{GetType().Namespace}] Trader name / Trader ID '{traderId}' is incorrect!", LogTextColor.Red);
                return;
            }
            var trader = traders[(MongoId)traderId];

            foreach (var (addBarterId, _) in barterConfig.BarterPrice)
            {
                var addBarter = GetItemIdByName(addBarterId);
                if (addBarter == null)
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Barter item of id '{addBarterId}' is incorrect! Item {itemId} was not added to trader", LogTextColor.Red);
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
            var recipes = hideoutCrafts.Production.Recipes;
            var recipeToCopy = recipes?.Where(e => e.Id == craftIdToCopy).ToList();
            var newRecipe = cloner.Clone(recipeToCopy?.FirstOrDefault());
            if (newRecipe?.Requirements is null)
            {
                logger.LogWithColor($"[{GetType().Namespace}] Can't find craft of id: {craftIdToCopy}", LogTextColor.Red);
                return;
            }
            newRecipe.Requirements = [newRecipe.Requirements.FirstOrDefault()];
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
            };
            newRecipe.Count = 1;
            newRecipe.ProductionTime = productionTime;
            newRecipe.Id = newCraftId;
            recipes?.Add(newRecipe);
        }

        public MongoId? GetTraderIdByName(string name)
        {
            var field = typeof(Traders).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.GetValue(null) is MongoId id)
            {
                return id;
            }
            if (!MongoId.IsValidMongoId(name) || !traders.TryGetValue(name, out _)) return null;

            return name;
        }

        public MongoId? GetItemIdByName(string name)
        {
            var field = typeof(ItemTpl).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field != null && field.GetValue(null) is MongoId id)
            {
                return id;
            }
            if (!MongoId.IsValidMongoId(name) || !items.TryGetValue(name, out _)) return null;

            return name;
        }
    }
}
