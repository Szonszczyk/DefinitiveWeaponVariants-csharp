using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Services;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.MarkedRooms
{
    internal class MarkedRoomsHelper(
        ISptLogger<DefinitiveWeaponVariants> logger,
        DatabaseService databaseService,
        ICloner cloner,
        ConfigLoader configLoader,
        ItemHelper itemHelper, RandomUtil randomUtil
    )
    {
        private readonly ConfigData modConfig = configLoader.Config;
        private readonly Dictionary<MongoId, List<Item>> variants = [];

        private readonly List<StaticAmmoDetails> caliber127x108details =
        [
            new StaticAmmoDetails
            {
                Tpl = "5cde8864d7f00c0010373be1",
                RelativeProbability = 1
            },
            new StaticAmmoDetails
            {
                Tpl = "5d2f2ab648f03550091993ca",
                RelativeProbability = 1
            },
        ];
        public void AddVariantToMarkedRoomsDatabase(List<Item> presetItems)
        {
            var items = cloner.Clone(presetItems);
            if (items?.Count == 0 || items is null) return;
            //var rootItem = items?.First();
            //if (rootItem is null || items is null) return;
            //items = itemHelper.ReparentItemAndChildren(rootItem, items);
            //if (items is null) return;
            //var newRootId = items[0].Id;
            //variants[(MongoId)newRootId] = items;
            variants[new MongoId()] = items;
        }
        public void AddVariantsToMarkedRooms()
        {
            
            if (modConfig.MarkedRoomsProbability == 0) return;
            if (modConfig.MarkedRoomsProbability < 0 || modConfig.MarkedRoomsProbability >= 1)
            {
                logger.LogWithColor($"[{GetType().Namespace}] Config option of MarkedRoomsProbability is incorrect, is: {modConfig.MarkedRoomsProbability}, should be between 0 and 1", LogTextColor.Red);
                return;
            }
            if (variants.Count == 0) return;
            logger.LogWithColor($"[{GetType().Namespace}] There are {variants.Count} possible weapons in Marked Rooms", LogTextColor.Cyan);
            var locations = databaseService.GetLocations().GetDictionary();

            foreach ((string locationId, Location location) in locations)
            {
                location.LooseLoot?.AddTransformer(looseLoot =>
                {
                    if (looseLoot is null || looseLoot.Spawnpoints is null) return looseLoot;

                    foreach (var spawnpoint in looseLoot.Spawnpoints)
                    {
                        if (spawnpoint is null || spawnpoint?.Template?.Id is null|| spawnpoint?.Template?.Items is null || spawnpoint?.ItemDistribution is null) return looseLoot;

                        var spawnpointTemplateItems = spawnpoint.Template.Items.ToList();
                        var itemDistribution = spawnpoint.ItemDistribution.ToList();

                        var spawnpointId = spawnpoint.Template.Id;

                        if (!MarkedRoomsIds.IsMarkedRoom(locationId.ToLowerInvariant(), spawnpointId)) continue;

                        double totalProbability = itemDistribution.Sum(item => item.RelativeProbability ?? 0);

                        if (totalProbability == 0) continue;

                        var probabilityOfAddedItems = modConfig.MarkedRoomsProbability / (1 - modConfig.MarkedRoomsProbability) * totalProbability;

                        var itemsAdded = 5;
                        for (int i = 0; i < itemsAdded; i++)
                        {
                            var random = new Random();
                            var variant = variants.ElementAt(random.Next(variants.Count));
                            var composedKey = variant.Key;
                            var items = cloner.Clone(variant.Value)!;

                            var rootItem = items?.First();
                            if (rootItem is null || items is null) continue;
                            items = itemHelper.ReparentItemAndChildren(rootItem, items);
                            items = AddCartridgesToMagazine(items, location);
                            rootItem = items.First();
                            var newComposedKey = rootItem.Id;

                            foreach (var item in items)
                            {
                                if (item.Id == newComposedKey)
                                {
                                    spawnpointTemplateItems.Add(new() { Id = item.Id, Template = item.Template, SlotId = item.SlotId, Upd = item.Upd, ComposedKey = newComposedKey });
                                }
                                else
                                {
                                    spawnpointTemplateItems.Add(new() { Id = item.Id, Template = item.Template, ParentId = item.ParentId, SlotId = item.SlotId, Upd = item.Upd });
                                }
                            }

                            LooseLootItemDistribution lliDistribution = new()
                            {
                                RelativeProbability = Math.Floor(probabilityOfAddedItems / itemsAdded),
                                ComposedKey = new ComposedKey
                                {
                                    Key = newComposedKey
                                }
                            };
                            itemDistribution.Add(lliDistribution);
                        }
                        spawnpoint.Template.Items = spawnpointTemplateItems;
                        spawnpoint.ItemDistribution = itemDistribution;
                    }

                    return looseLoot;
                });
            }
        }

        private List<Item> AddCartridgesToMagazine(List<Item> items, Location location)
        {
            var rootItem = items[0];
            Item? magazine = items.Find(x => x.SlotId == "mod_magazine");

            if (magazine is null) return items;

            TemplateItem? magTemplate = itemHelper.GetItem(magazine.Template).Value;
            TemplateItem? defaultWeapon = itemHelper.GetItem(rootItem.Template).Value;

            if (magTemplate is null || defaultWeapon is null) return items;

            List<Item> magazineWithCartridges = [magazine];

            Dictionary<string, IEnumerable<StaticAmmoDetails>>? staticAmmo = null;
            if (defaultWeapon?.Properties?.AmmoCaliber == "Caliber127x108")
            {
                staticAmmo = cloner.Clone(location.StaticAmmo);
                staticAmmo?.Add("Caliber127x108", caliber127x108details);
            }
            List<string> caliberList = new();

            if (defaultWeapon?.Properties?.Chambers?.Count() > 0)
            {
                caliberList = (from x in (defaultWeapon?.Properties?.Chambers?.First().Properties?.Filters?.First().Filter)?.Where((MongoId x) => itemHelper.GetItem(x).Key) select itemHelper.GetItem(x).Value?.Properties?.Caliber).ToList();
            }

            try {
                itemHelper.FillMagazineWithRandomCartridge(
                    magazineWithCartridges,
                    magTemplate,
                    staticAmmo ?? location.StaticAmmo,
                    caliberList.Count > 0 ? randomUtil.DrawRandomFromList(caliberList).First() : defaultWeapon?.Properties?.AmmoCaliber,
                    0.05,
                    defaultWeapon?.Properties?.DefAmmo,
                    defaultWeapon
                );
            } catch {
                return items;
            }

            var magIndex = items.IndexOf(magazine);
            items.RemoveAt(magIndex);
            items.InsertRange(magIndex, magazineWithCartridges);
            return items;
        }
    }
}
