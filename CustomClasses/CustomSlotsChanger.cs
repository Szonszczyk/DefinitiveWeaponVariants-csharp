using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.CustomClasses
{
    internal class CustomSlotsChanger(
        ISptLogger<DefinitiveWeaponVariants> logger,
        DatabaseService databaseService,
        ModDatabaseLoader modDatabaseLoader,
        ICloner cloner,
        IdDatabaseManager idDatabaseManager
    )
    {
        private readonly Dictionary<MongoId, TemplateItem> items = databaseService.GetItems();

        public List<Slot>? SlotsChanger(
            Dictionary<string, FilterSlotExtendedConfiguration>? slotConfig,
            TemplateItem copiedItem,
            NewItemFromCloneDetails newItem,
            string newItemName
        )
        {
            if (slotConfig != null && copiedItem.Properties != null && copiedItem.Properties.Slots != null)
            {
                var slots = new List<Slot>();

                foreach (var slot in copiedItem.Properties.Slots)
                {
                    if (slot == null) continue;
                    var newSlot = cloner.Clone(slot)!;
                    newSlot.Id = idDatabaseManager.GetCustomId($"{newItemName}:SLOT:{newSlot.Name}");
                    newSlot.Parent = newItem.NewId;
                    if (newSlot.Name != null && slotConfig.TryGetValue(newSlot.Name, out FilterSlotExtendedConfiguration? newFilterConfig))
                    {
                        var newFilter = CreateFilterFromConfiguration(newFilterConfig, newSlot.Name, "Slots", copiedItem);

                        if (newFilterConfig.BasedOn is { Property: var basedOnProperty } basedOn)
                        {
                            var value = copiedItem!.Properties.GetType()
                                .GetProperty(basedOnProperty)?
                                .GetValue(copiedItem.Properties)?
                                .ToString();

                            if (value != null && basedOn.Cases.TryGetValue(value, out var caseCfg))
                            {
                                newFilter = [.. newFilter, .. CreateFilterFromConfiguration(caseCfg, newSlot.Name, "Slots", copiedItem)];
                            }
                        }
                        if (newSlot?.Properties?.Filters != null)
                        {
                            newSlot.Properties.Filters.First().Filter = [.. newFilter];
                        }
                    }
                    slots.Add(newSlot!);
                }
                if (slots.Count > 0) return slots;
                return null;
            }
            return null;
        }

        public List<Slot>? ChambersChanger(
            FilterSlotExtendedConfiguration? chamberConfig,
            TemplateItem? copiedItem,
            NewItemFromCloneDetails newItem,
            string newItemName
        )
        {
            if (chamberConfig == null) return null;
            var newFilter = CreateFilterFromConfiguration(chamberConfig, "N/A", "Chambers", copiedItem);
            var chambers = new List<Slot>();
            if (copiedItem?.Properties?.Chambers?.Count() > 0)
            {
                foreach (var slot in copiedItem.Properties.Chambers)
                {
                    if (slot == null) continue;

                    var newSlot = cloner.Clone(slot)!;
                    newSlot.Id = idDatabaseManager.GetCustomId($"{newItemName}:CHAMBER:{newSlot.Name}");
                    newSlot.Parent = newItem.NewId;
                    if (newSlot?.Properties?.Filters != null)
                        newSlot.Properties.Filters.First().Filter = [.. newFilter];
                    chambers.Add(newSlot!);
                }
                return chambers;
            }
            return null;
        }
        
        public List<Slot>? CartridgesChanger(
            FilterSlotExtendedConfiguration? cartridgesConfig,
            TemplateItem? copiedItem,
            NewItemFromCloneDetails newItem,
            string newItemName
        )
        {
            if (cartridgesConfig == null) return null;
            if (cartridgesConfig.Count == null || cartridgesConfig.Count <= 0)
            {
                logger.LogWithColor($"[{GetType().Namespace}] Item '{newItemName}' have incorrect cartridges count {cartridgesConfig.Count}!", LogTextColor.Red);
                return null;
            }
            var newFilter = CreateFilterFromConfiguration(cartridgesConfig, "N/A", "Cartridges", copiedItem);
            if (newFilter.Count == 0) {
                logger.LogWithColor($"[{GetType().Namespace}] Item '{newItemName}' have no valid ammo!", LogTextColor.Red);
                return null;
            }
            var cartridges = new List<Slot>();
            if (copiedItem?.Properties?.Cartridges?.Count() > 0)
            {
                foreach (var slot in copiedItem.Properties.Cartridges)
                {
                    if (slot == null) continue;

                    var newSlot = cloner.Clone(slot)!;
                    newSlot.Id = idDatabaseManager.GetCustomId($"{newItemName}:CARTRIDGE:{newSlot.Name}");
                    newSlot.Parent = newItem.NewId;
                    newSlot.MaxCount = cartridgesConfig.Count;
                    if (newSlot?.Properties?.Filters != null)
                        newSlot.Properties.Filters.First().Filter = [.. newFilter];
                    cartridges.Add(newSlot!);
                }
                return cartridges;
            }
            return null;
        }

        public List<MongoId> CreateFilterFromConfiguration(FilterSlotConfiguration filterSlotConfig, string slotName, string type, TemplateItem? copiedItem)
        {
            var newFilter = new List<MongoId>();

            // Add copiedItem to FromWeapons if only Add property is present
            // Add property when used without Filter and FromWeapons is simply adding to existing filter, we need it
            if (filterSlotConfig.Filter is null && filterSlotConfig.FromWeapons is null && filterSlotConfig.Add != null && copiedItem is not null)
            {
                filterSlotConfig.FromWeapons = [copiedItem.Id];
            }

            // Add manually defined filters
            if (filterSlotConfig.Filter != null)
            {
                foreach (var filter in filterSlotConfig.Filter)
                {
                    var item = GetItemFromString(filter);
                    if (item != null) newFilter.Add(item.Id);
                }
            }

            // Add filters from weapons
            if (filterSlotConfig.FromWeapons != null)
            {
                foreach (var filter in filterSlotConfig.FromWeapons)
                {
                    TemplateItem? item = GetItemFromString(filter);
                    if (item == null) continue;

                    IEnumerable<MongoId>? filterFromWeapon = null;

                    var props = item.Properties;
                    if (props == null) continue;

                    switch (type)
                    {
                        case "Slots":
                            filterFromWeapon = props.Slots?.FirstOrDefault(t => t?.Name == slotName)?.Properties?.Filters?.First()?.Filter;
                            break;

                        case "Chambers":
                            filterFromWeapon = props.Chambers?.First()?.Properties?.Filters?.First()?.Filter;
                            break;

                        case "Cartridges":
                            filterFromWeapon = props.Cartridges?.First()?.Properties?.Filters?.First()?.Filter;
                            filterFromWeapon ??= props.Chambers?.First()?.Properties?.Filters?.First()?.Filter;
                            break;

                        default:
                            continue; // Unknown type — skip
                    }

                    if (filterFromWeapon != null)
                    {
                        var clonedFilters = cloner.Clone(filterFromWeapon);
                        if (clonedFilters != null) newFilter.AddRange(clonedFilters);
                    }
                    else
                    {
                        logger.LogWithColor($"[{GetType().Namespace}] FromWeapon (item) '{filter}' found, but couldn't get filters from {type}!", LogTextColor.Red);
                    }
                }
            }

            // Add additional filters
            if (filterSlotConfig.Add != null)
            {
                foreach (var filter in filterSlotConfig.Add)
                {
                    var item = GetItemFromString(filter);
                    if (item != null) newFilter.Add(item.Id);
                }
            }

            return newFilter;
        }

        public TemplateItem? GetItemFromString(string text)
        {
            if (MongoId.IsValidMongoId(text))
            {
                if (items.TryGetValue(text, out var mongoIdItem)) return mongoIdItem;
            }
            else
            {
                if (modDatabaseLoader.DbShortnames.TryGetValue(text, out var weaponId))
                {
                    if (items.TryGetValue(weaponId, out var weaponItem)) return weaponItem;
                }
                if (idDatabaseManager.DbIds.TryGetValue($"{text}:ID", out var idDatabaseId))
                {
                    if (items.TryGetValue(idDatabaseId, out var idDatabaseItem)) return idDatabaseItem;
                }
            }
            logger.LogWithColor($"[{GetType().Namespace}] Item '{text}' not found", LogTextColor.Red);
            return null;
        }
    }
}
