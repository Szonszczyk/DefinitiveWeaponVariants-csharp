using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;

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

        public List<Slot>? CoreSlotAdder(
            List<Slot>? slotsBefore,
            TemplateItem copiedItem,
            List<MongoId> newFilter,
            NewItemFromCloneDetails newItem,
            string newItemName
        )
        {
            var slots = slotsBefore ?? RemapSlots(copiedItem?.Properties?.Slots?.ToList(), newItem.NewId!);
            if (slots is null) return null;
            var addedSlot = new Slot
            {
                Name = "mod_core",
                Id = new MongoId(),
                Parent = newItem.NewId,
                Properties = new SlotProperties()
                //Required = true
            };
            var filterSlot = new SlotFilter { Filter = [.. newFilter] };
            addedSlot.Properties.Filters = new List<SlotFilter> { filterSlot };
            slots.Add(addedSlot);
            return slots;
        }

        public List<Slot>? SlotsChanger(
            Dictionary<string, FilterSlotExtendedConfiguration>? slotConfig,
            TemplateItem copiedItem,
            NewItemFromCloneDetails newItem
        )
        {
            if (slotConfig is null) return null;
            var slots = RemapSlots(copiedItem?.Properties?.Slots?.ToList(), newItem.NewId!);
            if (slots is null) return null;

            var slotsToRemove = new List<Slot>();
            foreach (var slot in slots)
            {
                if (slot.Name != null && slotConfig.TryGetValue(slot.Name, out FilterSlotExtendedConfiguration? newFilterConfig))
                {
                    var newFilter = CreateFilterFromConfiguration(newFilterConfig, slot.Name, slot.Name.Contains("camora_") ? "Chambers" : "Slots", copiedItem);
                    if (newFilterConfig.BasedOn is { Property: var basedOnProperty } basedOn)
                    {
                        var value = copiedItem!.Properties.GetType()
                            .GetProperty(basedOnProperty)?
                            .GetValue(copiedItem.Properties)?
                            .ToString();

                        if (value != null && basedOn.Cases.TryGetValue(value, out var caseCfg))
                        {
                            newFilter = [.. newFilter, .. CreateFilterFromConfiguration(caseCfg, slot.Name, slot.Name.Contains("camora_") ? "Chambers" : "Slots", copiedItem)];
                        }
                    }
                    if (newFilter.Count == 0)
                    {
                        slotsToRemove.Add(slot);
                        continue;
                    }
                    if (slot?.Properties?.Filters != null)
                    {
                        slot.Properties.Filters.First().Filter = [.. newFilter];
                    }
                }
            }
            foreach(var slot in slotsToRemove)
            {
                slots.Remove(slot);
            }
            return slots;
        }

        public List<Slot>? ChambersChanger(
            FilterSlotExtendedConfiguration? chamberConfig,
            TemplateItem? copiedItem,
            NewItemFromCloneDetails newItem,
            string newItemName
        )
        {
            if (chamberConfig == null) return null;
            if (copiedItem?.Properties?.Chambers is null || copiedItem?.Properties?.Chambers?.Count() == 0) return null;
            var newFilter = CreateFilterFromConfiguration(chamberConfig, "N/A", "Chambers", copiedItem);
            if (newFilter.Count == 0)
            {
                logger.LogWithColor($"[{GetType().Namespace}] Item '{newItemName}' have no valid ammo in chambers!", LogTextColor.Red);
                return null;
            }
            var chambers = RemapSlots(copiedItem?.Properties?.Chambers?.ToList(), newItem.NewId!);
            if (chambers is not null && chambers.Count > 0)
            {
                foreach (var chamber in chambers)
                {
                    if (chamber?.Properties?.Filters != null)
                        chamber.Properties.Filters.First().Filter = [.. newFilter];
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
            var cartridges = RemapSlots(copiedItem?.Properties?.Cartridges?.ToList(), newItem.NewId!);
            if (cartridges is not null && cartridges.Count > 0)
            {
                foreach (var cartridge in cartridges)
                {
                    cartridge.MaxCount = cartridgesConfig.Count;
                    if (cartridge?.Properties?.Filters != null)
                        cartridge.Properties.Filters.First().Filter = [.. newFilter];
                }
                return cartridges;
            }
            return null;
        }

        public List<MongoId> CreateFilterFromConfiguration(FilterSlotConfiguration filterSlotConfig, string slotName, string type, TemplateItem? copiedItem)
        {
            var newFilter = new List<MongoId>();
            var config = cloner.Clone(filterSlotConfig);
            if (config is null) return newFilter;

            // Add copiedItem to FromWeapons if only Add property is present
            // Add property when used without Filter and FromWeapons is simply adding to existing filter, we need it
            if (config.Filter is null && config.FromWeapons is null && config.Add != null && copiedItem is not null)
            {
                config.FromWeapons = [copiedItem.Id];
            }

            // Add manually defined filters
            if (config.Filter != null)
            {
                foreach (var filter in config.Filter)
                {
                    var item = GetItemFromString(filter);
                    if (item != null) newFilter.Add(item.Id);
                }
            }

            // Add filters from weapons
            if (config.FromWeapons != null)
            {
                foreach (var filter in config.FromWeapons)
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
            if (config.Add != null)
            {
                foreach (var filter in config.Add)
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
                var field = typeof(ItemTpl).GetField(text, BindingFlags.Public | BindingFlags.Static);
                if (field != null && field.GetValue(null) is MongoId id)
                {
                    if (items.TryGetValue(id, out var idFromTemplate)) return idFromTemplate;
                }
            }
            logger.LogWithColor($"[{GetType().Namespace}] Item '{text}' not found", LogTextColor.Red);
            return null;
        }

        private List<Slot>? RemapSlots(List<Slot>? oldSlots, string id)
        {
            var slots = cloner.Clone(oldSlots);
            if (slots is null) return null;
            foreach (var slot in slots)
            {
                slot.Id = new MongoId();
                slot.Parent = id;
            }
            return slots;
        }
    }
}
