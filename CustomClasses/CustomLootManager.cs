using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.CustomClasses;

[Injectable(InjectionType.Singleton)]
public class CustomLootManager(
    CustomLogger logger,
    ICloner cloner,
    ConfigData config,
    ItemHelper itemHelper,
    RandomUtil randomUtil,
    IdDatabaseManager idDatabaseManager,
    ModDataStorage modDataStorage
)
{
    private readonly ConfigData modConfig = config;

    private class WeightDataForLoot
    {
        public int TotalWeight { get; set; } = 0;
        public List<string> Qualities { get; set; } = [];
        public float Probability { get; set; } = 0;
        public int TotalWeapons { get; set; } = 0;
    }

    private Dictionary<string, WeightDataForLoot> GetWeightDataForLoot()
    {
        WeightDataForLoot Build(
            Dictionary<string, bool> source,
            float probability)
        {
            var data = new WeightDataForLoot { Probability = probability };
            foreach (var (q, enabled) in source.Where(x => x.Value))
            {
                if (!modDataStorage.VariantPresets.TryGetValue(q, out var variantItems) || variantItems.Count == 0) continue;
                data.TotalWeight += modConfig.QualityWeights[q];
                data.Qualities.Add(q);
                data.TotalWeapons += variantItems.Count;
            }
            return data;
        }
        return new()
        {
            ["Marked"] = Build(modConfig.Marked, modConfig.MarkedRoomsProbability),
            ["StaticLoot"] = Build(modConfig.StaticLoot, modConfig.StaticLootProbability),
        };
    }

    private (string key, List<SptLootItem> weapon) GetRandomWeaponByQuality(string quality, Location location)
    {
        var allPossibleWeapons = modDataStorage.VariantPresets[quality];
        var random = new Random();
        var variant = allPossibleWeapons.ElementAt(random.Next(allPossibleWeapons.Count));

        List<SptLootItem> weapon = [];
        var items = cloner.Clone(variant)!;

        var rootItem = items.First();
        items = itemHelper.ReparentItemAndChildren(rootItem, items);
        items = AddCartridgesToMagazine(items, location);
        rootItem = items.First();
        var newComposedKey = rootItem.Id;

        foreach (var item in items)
        {
            if (item.Id == newComposedKey)
            {
                weapon.Add(new() { Id = item.Id, Template = item.Template, SlotId = item.SlotId, Upd = item.Upd, ComposedKey = newComposedKey });
            }
            else
            {
                weapon.Add(new() { Id = item.Id, Template = item.Template, ParentId = item.ParentId, SlotId = item.SlotId, Upd = item.Upd });
            }
        }
        return (newComposedKey.ToString(), weapon);
    }

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
    public void AddVariantsToLooseLoot()
    {
        var weights = GetWeightDataForLoot();
        if (weights["Marked"].Probability < 0 || weights["Marked"].Probability >= 1)
        {
            logger.Error($"Config option of MarkedRoomsProbability is incorrect, is: {weights["Marked"].Probability}, should be between 0 and 1. Disabling adding variants to Marked rooms!");
            weights["Marked"].Probability = 0;
        }
        var locations = modDataStorage.LocationsData.GetDictionary();

        
        logger.Ok($"There are {weights["Marked"].TotalWeapons} possible weapons in Marked Rooms");
        foreach ((string locationId, Location location) in locations)
        {
            // Add info about 12.7x108 caliber
            location.StaticAmmo?.Add("Caliber127x108", caliber127x108details);

            if (!((weights["Marked"].Probability == 0 && weights["LooseLoot"].Probability == 0) || (weights["Marked"].TotalWeapons == 0 && weights["LooseLoot"].TotalWeapons == 0)))
            {
                location.LooseLoot?.AddTransformer(looseLoot =>
                {
                    if (looseLoot is null || looseLoot.Spawnpoints is null) return looseLoot;

                    foreach (var spawnpoint in looseLoot.Spawnpoints)
                    {
                        if (spawnpoint is null || spawnpoint?.Template?.Id is null || spawnpoint?.Template?.Items is null || spawnpoint?.ItemDistribution is null) return looseLoot;

                        var spawnpointTemplateItems = spawnpoint.Template.Items.ToList();
                        var itemDistribution = spawnpoint.ItemDistribution.ToList();
                        var spawnpointId = spawnpoint.Template.Id;

                        // Add variants to Marked room
                        if (weights["Marked"].Probability == 0 || weights["Marked"].TotalWeapons == 0 || !MarkedRoomsIds.IsMarkedRoom(locationId.ToLowerInvariant(), spawnpointId)) continue;
                        double totalProbability = itemDistribution.Sum(item => item.RelativeProbability ?? 0);
                        if (totalProbability == 0) continue;
                        var probabilityOfAddedItems = weights["Marked"].Probability / (1 - weights["Marked"].Probability) * totalProbability;
                        foreach (var quality in weights["Marked"].Qualities)
                        {
                            (string composedKey, List<SptLootItem> weapon) = GetRandomWeaponByQuality(quality, location);
                            spawnpointTemplateItems.AddRange(weapon);

                            itemDistribution.Add(new LooseLootItemDistribution
                            {
                                RelativeProbability = Math.Floor(probabilityOfAddedItems * ((double)modConfig.QualityWeights[quality] / (double)weights["Marked"].TotalWeight)),
                                ComposedKey = new ComposedKey
                                {
                                    Key = composedKey
                                }
                            });
                            logger.Debug($"Added {composedKey}/{quality} with {Math.Floor(probabilityOfAddedItems * ((double)modConfig.QualityWeights[quality] / (double)weights["Marked"].TotalWeight))}(QWEIGHT:{modConfig.QualityWeights[quality]}/TOTALWEIGHT:{weights["Marked"].TotalWeight})(MAX:{totalProbability},ADDED:{probabilityOfAddedItems}) to {spawnpointId}");
                        }

                        // TODO: Add variants to loose loot
                        //      We need to get spawn point IDs of only weapon vanilla spawns - hardcoded to not replace other modded weapons
                        spawnpoint.Template.Items = spawnpointTemplateItems;
                        spawnpoint.ItemDistribution = itemDistribution;
                    }

                    return looseLoot;
                });
            }

            List<string> containersForWeapons =
            [
                "5909d5ef86f77467974efbd8", // "LOOTCONTAINER_WEAPON_BOX_5X2"
                "5909d76c86f77471e53d2adf", // "LOOTCONTAINER_WEAPON_BOX_6X3"
                "5909d7cf86f77470ee57d75a", // "LOOTCONTAINER_WEAPON_BOX_4X4"
                "5909d89086f77472591234a0", // "LOOTCONTAINER_WEAPON_BOX_5X5"
                "578f87a3245977356274f2cb", // "LOOTCONTAINER_DUFFLE_BAG"
                "578f87ad245977356274f2cc", // "LOOTCONTAINER_WOODEN_CRATE"
                "5d6d2b5486f774785c2ba8ea", // "LOOTCONTAINER_GROUND_CACHE"
                "5d6d2bb386f774785b07a77a"  // "LOOTCONTAINER_BURIED_BARREL_CACHE"
            ];
            List<string> containersForPackage =
            [
                "578f8778245977358849a9b5", // "LOOTCONTAINER_JACKET"
                "5909e4b686f7747f5b744fa4", // "LOOTCONTAINER_DEAD_SCAV"
                "59139c2186f77411564f8e42", // "LOOTCONTAINER_PC_BLOCK"
                "5c052cea86f7746b2101e8d8", // "LOOTCONTAINER_PLASTIC_SUITCASE"
                "578f8782245977354405a1e3"  // "LOOTCONTAINER_SAFE"
            ];
            var unknownPackageId = idDatabaseManager.GetCustomId($"Unknown Variant Weapon Core Package:ID");
            if (weights["StaticLoot"].Probability != 0 && weights["StaticLoot"].TotalWeapons != 0)
            {
                location.StaticLoot?.AddTransformer(StaticLoot =>
                {
                    if (StaticLoot is null) return StaticLoot;
                    foreach ((MongoId containerId, StaticLootDetails container) in StaticLoot)
                    {
                        if (container is null) continue;
                        if (containersForWeapons.Contains(containerId) || containersForPackage.Contains(containerId))
                        {
                            container.ItemCountDistribution.ToList().ForEach(e => e.Count += 1);
                            var totalWeightOfItemsCount = container.ItemCountDistribution.Sum(x => x.RelativeProbability);
                            if (totalWeightOfItemsCount == 0 || totalWeightOfItemsCount is null) continue;
                            var amountOfItems = container.ItemCountDistribution.Sum(x => x.Count * x.RelativeProbability) / (double)totalWeightOfItemsCount;
                            var itemDistribution = container.ItemDistribution.ToList();
                            double totalProbability = itemDistribution.Sum(item => item.RelativeProbability ?? 0);
                            if (totalProbability == 0) continue;
                            if (containersForWeapons.Contains(containerId))
                            {
                                var probabilityOfAddedItems = weights["StaticLoot"].Probability / (1 - weights["StaticLoot"].Probability) * totalProbability;
                                foreach (var quality in weights["StaticLoot"].Qualities)
                                {
                                    var allPossibleWeapons = modDataStorage.VariantIdsByQuality[quality];
                                    foreach (var weaponId in allPossibleWeapons)
                                    {
                                        itemDistribution.Add(new ItemDistribution
                                        {
                                            Tpl = weaponId,
                                            RelativeProbability = (float?)((float)Math.Floor(probabilityOfAddedItems * ((double)modConfig.QualityWeights[quality] / (double)weights["StaticLoot"].TotalWeight)) / amountOfItems / allPossibleWeapons.Count),
                                        });
                                    }
                                }
                            }
                            if (containersForPackage.Contains(containerId) && modConfig.VariantCoresEnabled)
                            {
                                var probabilityOfAddedItems = modConfig.VariantCores.UnknownPackage.Probability / (1 - modConfig.VariantCores.UnknownPackage.Probability) * totalProbability;
                                
                                itemDistribution.Add(new ItemDistribution
                                {
                                    Tpl = unknownPackageId,
                                    RelativeProbability = (float?)(probabilityOfAddedItems / amountOfItems),
                                });
                            }

                            container.ItemDistribution = itemDistribution;
                        }
                    }
                    return StaticLoot;
                });
            }
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

        List<string> caliberList = new();

        if (defaultWeapon?.Properties?.Chambers?.Count() > 0)
        {
            caliberList = (from x in (defaultWeapon?.Properties?.Chambers?.First().Properties?.Filters?.First().Filter)?.Where((x) => itemHelper.GetItem(x).Key) select itemHelper.GetItem(x).Value?.Properties?.Caliber).ToList();
        }

        try {
            itemHelper.FillMagazineWithRandomCartridge(
                magazineWithCartridges,
                magTemplate,
                location.StaticAmmo,
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

    public void CreateLootpoolForBlindBoxes()
    {
        foreach (var (quality, enabled) in modConfig.Generate)
        {
            if (!enabled) continue;
            var allWeaponsInData = modDataStorage.VariantPresets[quality];
            Dictionary<MongoId, double> weapons = [];
            foreach (var weapon in allWeaponsInData)
            {
                var rootItem = weapon.First();
                weapons.Add(rootItem.Template, 1);
            }
            if (idDatabaseManager.DbIds.TryGetValue($"{quality} Quality Variant Weapon Blind Box:ID", out var idDatabaseId))
            {
                (bool find, TemplateItem? item) = itemHelper.GetItem(idDatabaseId);
                if (find && item is not null)
                {
                    modDataStorage.InventoryConfigData.RandomLootContainers.Add(idDatabaseId, new RewardDetails
                    {
                        RewardCount = 1,
                        FoundInRaid = false,
                        RewardTplPool = weapons
                    });
                    // Fix found on MoxoPixel-Painter
                    item.Name = idDatabaseId;
                }
            }
        }
    }

    

    public void AddCoresToBotPockets()
    {
        if (!modConfig.VariantCoresEnabled) return;
        var bots = modDataStorage.Bots;
        foreach (var (botName, prob) in modConfig.VariantCores.Normal.FoundOnEnemies)
        {
            if (prob <= 0) continue;

            bots.Types.TryGetValue(botName, out var bot);
            if (bot is null)
            {
                logger.Warning($"Bot name '{botName}' is incorrect. Bot names can be found in SPT_Data\\database\\bots\\types");
                continue;
            }
            var pockets = bot.BotInventory.Items.Pockets;
            var totalProbability = pockets.Sum(item => item.Value);
            var probabilityOfAddedItems = prob / (1 - prob) * totalProbability;
            var qualityTotal = modConfig.QualityWeights.Sum(item => item.Value);

            foreach (var (quality, enabled) in modConfig.Generate)
            {
                if (!enabled) continue;
                if (idDatabaseManager.DbIds.TryGetValue($"{quality} Quality Variant Core:ID", out var idDatabaseId))
                {
                    (bool find, TemplateItem? item) = itemHelper.GetItem(idDatabaseId);
                    
                    if (find && item is not null)
                    {
                        pockets.Add(idDatabaseId, Math.Ceiling(((float)modConfig.QualityWeights[quality] / (float)qualityTotal) * probabilityOfAddedItems)); 
                        logger.Debug($"Added {quality} Quality Variant Core with {Math.Ceiling(((float)modConfig.QualityWeights[quality] / (float)qualityTotal) * probabilityOfAddedItems)}(QWEIGHT:{modConfig.QualityWeights[quality]}/TOTALWEIGHT:{qualityTotal})(MAX:{totalProbability},ADDED:{probabilityOfAddedItems}) to Scav pockets");
                    }
                }
            }
        }
    }
}
