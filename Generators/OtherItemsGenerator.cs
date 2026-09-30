using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.Generators;

[Injectable(InjectionType.Singleton)]
public class OtherItemsGenerator(
    IdDatabaseManager idDatabaseManager,
    CustomSlotsChanger customSlotsChanger,
    ConfigData config,
    ICloner cloner,
    ModDataStorage modDataStorage,
    ItemGenerator itemGenerator,
    ItemHelper itemHelper,
    CustomItemCreator customItemCreator,
    InventoryConfig inventoryConfig,
    CustomLocales customLocales
)
{
    private readonly ConfigData modConfig = config;
    private Dictionary<string, int> UnknownPackageLootpool { get; set; } = [];

    private Dictionary<MongoId, double> UnknownPackageLootpoolIds { get; set; } = [];

    private TemplateItemProperties VariantCorePropertiesOverride { get; } = new()
    {
        StackMaxSize = config.VariantCores.General.StackMaxSize,
        Prefab = new Prefab
        {
            Path = "assets/content/items/barter/cpu/item_cpu.bundle",
            Rcid = ""
        },
        ExtraSizeDown = 0,
        ToolModdable = true,
        RaidModdable = true,
        Recoil = 0,
        Slots = []
    };

    public void GenerateOtherItems()
    {
        GenerateNormalCores();
        GenerateLockedCores();
        GenerateBlindBoxes();
        GenerateUniversalCores();

        GenerateUnknownPackage();

        GenerateCoreHolder();
        GenerateCoreCase();

        CreateLootpoolForUnknownPackage();
    }

    private void GenerateNormalCores()
    {
        var corePropertiesOverride = cloner.Clone(VariantCorePropertiesOverride);
        if (corePropertiesOverride is null) return;
        corePropertiesOverride.Ergonomics = modConfig.VariantCores.Normal.Properties.Ergonomics;
        foreach (var (quality, enabled) in modConfig.Generate)
        {
            // Generate normal core
            var variant = new VariantConfiguration
            {
                Description = "{CoreItem.Normal.Description}",
                ShortName = $"{{Quality.{quality}.ShortName}} {{CoreItem.Normal.ShortName}}",
                ItemTplToClone = "58d2912286f7744e27117493",
                HandbookPriceRoubles = modConfig.VariantCores.General.Price[quality] * modConfig.VariantCores.Normal.PriceMultiplier,
                Rarity = quality,
                VariantType = quality
            };
            if (enabled && modConfig.VariantCoresEnabled && modConfig.VariantCores.Normal.Buyable)
            {
                variant.Barter = modConfig.VariantCores.Normal.Barter;
                variant.Barter.BarterPrice = [];
                variant.Barter.BarterPrice.Add("5449016a4bdc2d6f028b456f", modConfig.VariantCores.General.Price[quality] * modConfig.VariantCores.Normal.PriceMultiplier);
            }
            var newId = itemGenerator.GenerateItem(
                $"{quality} Quality Variant Core",
                $"{{Quality.{quality}.Name}} {{CoreItem.Normal.Name}}",
                variant,
                $"<color={{rarity.Color}}><b>{{Quality.{quality}.Name}} {{CoreItem.Normal.Name}}</b></color>",
                $"{{CoreItem.Normal.Explanation}}.{(modConfig.VariantCores.Normal.FoundOnEnemies.TryGetValue("assault", out _) ? "\n{CoreItem.Normal.AddDesc}" : "")}",
                corePropertiesOverride
            );
            if (newId is null) { continue; }
            modDataStorage.AddCoreToStorage(newId, quality, "normal");
            RarityData rarity = RaritySettings.GetByName(variant.Rarity);
            UnknownPackageLootpool.Add($"<color={rarity.Color}><b>{{Quality.{quality}.Name}} {{CoreItem.Normal.Name}} (90%/10% {{CoreItem.Locked.ShortName}})</b></color>", modConfig.QualityWeights[quality] * 9);
            UnknownPackageLootpoolIds.Add(newId, modConfig.QualityWeights[quality] * 9);
        }
        if (modConfig.VariantCores.Normal.Upgradable && modConfig.VariantCoresEnabled)
        {
            for (var i = 1; i < RaritySettings.RarityList().Count; i++)
            {
                var qualityNow = RaritySettings.RarityList()[i];
                var qualityPrev = RaritySettings.RarityList()[i - 1];
                modConfig.Generate.TryGetValue(qualityNow, out var qualityNowEnabled);
                modConfig.Generate.TryGetValue(qualityPrev, out var qualityPrevEnabled);
                if (!qualityNowEnabled || !qualityPrevEnabled) { continue; }
                var nowCore = customSlotsChanger.GetItemFromString($"{qualityNow} Quality Variant Core")!;
                var prevCore = customSlotsChanger.GetItemFromString($"{qualityPrev} Quality Variant Core")!;
                var barterConfig = cloner.Clone(modConfig.VariantCores.Normal.UpgradableOptions.Barter);
                if (barterConfig is null) continue;
                barterConfig.BarterPrice.Add((string)prevCore.Id, modConfig.VariantCores.Normal.UpgradableOptions.Ratio);
                if (modConfig.AmonyaTraderMode) barterConfig.TraderId = "ee840a5ba014e9c5478d5ccd";
                customItemCreator.AddItemToTrader(nowCore.Id, barterConfig);
                if (qualityNow == modConfig.VariantCores.Normal.UpgradableOptions.UpToQuality) break;
            }
        }
    }

    private void GenerateLockedCores()
    {
        var corePropertiesOverride = cloner.Clone(VariantCorePropertiesOverride);
        if (corePropertiesOverride is null) return;
        corePropertiesOverride.Ergonomics = modConfig.VariantCores.Locked.Properties.Ergonomics;
        corePropertiesOverride.Accuracy = modConfig.VariantCores.Locked.Properties.Accuracy;
        foreach (var (quality, enabled) in modConfig.Generate)
        {
            var variant = new VariantConfiguration
            {
                Description = "{CoreItem.Locked.Description}",
                ShortName = $"{{Quality.{quality}.ShortName}} {{CoreItem.Locked.ShortName}}",
                ItemTplToClone = "58d2912286f7744e27117493",
                HandbookPriceRoubles = modConfig.VariantCores.General.Price[quality] * modConfig.VariantCores.Locked.PriceMultiplier,
                Rarity = quality,
                VariantType = quality
            };
            var normalCoreId = idDatabaseManager.GetCustomId($"{quality} Quality Variant Core:ID");
            if (enabled && modConfig.VariantCores.Locked.Upgradable && modConfig.VariantCoresEnabled)
            {
                variant.Barter = modConfig.VariantCores.Locked.UpgradableOptions.Barter;
                variant.Barter.BarterPrice = [];
                variant.Barter.BarterPrice.Add(normalCoreId, modConfig.VariantCores.Locked.UpgradableOptions.Ratio);
            }
            var newId = itemGenerator.GenerateItem(
                $"{quality} Quality Variant Core (Locked)",
                $"{{Quality.{quality}.Name}} {{CoreItem.Locked.Name}}", 
                variant,
                $"<color={{rarity.Color}}><b>{{Quality.{quality}.Name}} {{CoreItem.Locked.Name}}</b></color>",
                "{CoreItem.Locked.Explanation}",
                corePropertiesOverride
            );
            if (newId is null) { continue; }
            modDataStorage.AddCoreToStorage(newId, quality, "normal");
            UnknownPackageLootpoolIds.Add(newId, modConfig.QualityWeights[quality] * 1);
            customItemCreator.CreateCultistCircleCraft(
                [normalCoreId, normalCoreId, normalCoreId],
                [newId],
                10,
                true
            );
        }
    }

    private void GenerateBlindBoxes()
    {
        foreach (var (quality, enabled) in modConfig.Generate)
        {
            customLocales.RegisterTag("Quality", $"{{Quality.{quality}.Name}}");
            var variant = new VariantConfiguration
            {
                Description = "{CoreItem.BBox.Description}",
                ShortName = $"{{Quality.{quality}.Name}} {{CoreItem.BBox.ShortName}}",
                ItemTplToClone = "6489b2b131a2135f0d7d0fcb",
                HandbookPriceRoubles = modConfig.VariantCores.General.Price[quality] * modConfig.VariantCores.BlindBoxes.PriceMultiplier,
                Rarity = quality,
                VariantType = quality
            };
            var coreId = customSlotsChanger.GetItemFromString($"{quality} Quality Variant Core")?.Id ?? "5449016a4bdc2d6f028b456f";
            if (enabled && modConfig.BlindBoxesEnabled)
            {
                var barter = cloner.Clone(modConfig.VariantCores.BlindBoxes.Barter);
                barter ??= new CustomBarterConfig();
                if (modConfig.VariantCoresEnabled)
                    barter.BarterPrice.Add(coreId, modConfig.VariantCores.BlindBoxes.CoresPrice);
                else
                    barter.BarterPrice.Add("MONEY_ROUBLES", modConfig.VariantCores.General.Price[quality] * modConfig.VariantCores.BlindBoxes.PriceMultiplier);
                variant.Barter = barter;
            }
            var newId = itemGenerator.GenerateItem(
                $"{quality} Quality Variant Weapon Blind Box",
                $"{{Quality.{quality}.Name}} {{CoreItem.BBox.Name}}", 
                variant,
                "{CoreItem.BBox.AddDesc} <color={rarity.Color}><b>{Quality} {Other.Word.Quality} {Other.Word.Variant}</b></color>",
                "{CoreItem.BBox.Explanation}",
                new TemplateItemProperties
                {
                    Width = modConfig.VariantCores.BlindBoxes.Properties.Width,
                    Height = modConfig.VariantCores.BlindBoxes.Properties.Height
                }
            );
            if (newId is null) { continue; }
            modDataStorage.AddCoreToStorage(newId, quality, "other");
        }
    }

    private void GenerateUniversalCores()
    {
        var corePropertiesOverride = cloner.Clone(VariantCorePropertiesOverride);
        if (corePropertiesOverride is null) return;
        corePropertiesOverride.Ergonomics = modConfig.VariantCores.Universal.Properties.Ergonomics;
        corePropertiesOverride.Recoil = modConfig.VariantCores.Universal.Properties.Recoil;
        for (int i = 0; i < 8; i++)
        {
            var universalCorePropertiesOverride = cloner.Clone(corePropertiesOverride);
            if (universalCorePropertiesOverride is null) { continue; }

            universalCorePropertiesOverride.Ergonomics += (i - 3);
            universalCorePropertiesOverride.Recoil -= (8 - i);

            var newId = itemGenerator.GenerateItem(
                $"Universal Variant Weapon Core v1.{i}",
                $"{{CoreItem.UCore.Name}}{i}",
                new VariantConfiguration
                {
                    Description = "{CoreItem.UCore.Description}",
                    ShortName = $"{{CoreItem.UCore.ShortName}}{i}",
                    ItemTplToClone = "58d2912286f7744e27117493",
                    HandbookPriceRoubles = modConfig.VariantCores.General.Price["Unique"] * 5,
                    Rarity = "Unknown",
                    VariantType = "Unknown"
                },
                "<color={rarity.Color}><b>{CoreItem.UCore.AddDesc}</b></color>",
                "{CoreItem.UCore.Explanation}",
                universalCorePropertiesOverride
            );
            if (newId is null) { continue; }
            modDataStorage.AddCoreToStorage(newId, "Unknown", "universal");
            RarityData rarity = RaritySettings.GetByName("Unknown");
            UnknownPackageLootpool.Add($"<color={rarity.Color}><b>{{CoreItem.UCore.Name}}{i} ({{Other.Word.Ergonomics}} +{universalCorePropertiesOverride.Ergonomics}, {{Other.Word.Recoil}} {universalCorePropertiesOverride.Recoil}%)</b></color>", 3);
            UnknownPackageLootpoolIds.Add(newId, 3);
        }
    }
    private void GenerateCoreHolder()
    {
        var priceCoreId = customSlotsChanger.GetItemFromString($"{modConfig.VariantCores.ExpeditionaryCase.PriceCoreQuality} Quality Variant Core");
        var handbookPrice = priceCoreId == null ? modConfig.VariantCores.General.Price["Baseline"] * modConfig.VariantCores.ExpeditionaryCase.PriceCoreAmount : modConfig.VariantCores.General.Price[modConfig.VariantCores.ExpeditionaryCase.PriceCoreQuality] * modConfig.VariantCores.ExpeditionaryCase.PriceCoreAmount;
        var variant = new VariantConfiguration
        {
            Description = "{CoreItem.ExpCase.Description}",
            ShortName = "{CoreItem.ExpCase.ShortName}",
            ItemTplToClone = "5e2af55f86f7746d4159f07c",
            HandbookPriceRoubles = handbookPrice,
            Rarity = "Unknown",
            VariantType = "Unknown",
            Barter = modConfig.VariantCores.ExpeditionaryCase.Barter
        };

        if (priceCoreId is not null)
        {
            variant.Barter.BarterPrice.Add(priceCoreId.Id, 12);
        }
        else
        {
            variant.Barter.BarterPrice.Add("MONEY_ROUBLES", handbookPrice);
        }
        if (!modConfig.VariantCoresEnabled)
        {
            variant.Barter.LoyalLevel = 0;
        }

        var newGrids = new List<Grid>();
        foreach (var (quality, idList) in modDataStorage.CoresByQuality)
        {
            Grid columnCaseGrid = new()
            {
                Id = idDatabaseManager.GetCustomId($"EVCC:{quality}:Grid:ID"),
                Name = $"EVCC:{quality}:Grid",
                Parent = idDatabaseManager.GetCustomId($"Expeditionary Variant Core Case:ID"),
                Prototype = "55d329c24bdc2d892f8b4567",
                Properties = new()
                {
                    CellsH = modConfig.VariantCores.ExpeditionaryCase.GridProperties.CellsH,
                    CellsV = modConfig.VariantCores.ExpeditionaryCase.GridProperties.CellsV,
                    Filters = [new GridFilter { Filter = [.. idList] }],
                    IsSortingTable = false,
                    MaxCount = 0,
                    MaxWeight = 0,
                    MinCount = 0
                }
            };
            newGrids.Add(columnCaseGrid);
        }
        var newId = itemGenerator.GenerateItem(
            $"Expeditionary Variant Core Case",
            "{CoreItem.ExpCase.Name}",
            variant,
            "<color={rarity.Color}><b>{CoreItem.ExpCase.Name}</b></color>",
            "{CoreItem.ExpCase.Explanation}",
            new TemplateItemProperties
            {
                Width = modConfig.VariantCores.ExpeditionaryCase.Properties.Width,
                Height = modConfig.VariantCores.ExpeditionaryCase.Properties.Height,
                Grids = newGrids,
                Weight = modConfig.VariantCores.ExpeditionaryCase.Properties.Weight
            }
        );
        if (newId is not null)
        {
            customItemCreator.AddItemToSecureContainer(newId);
        }
    }

    private void GenerateCoreCase()
    {
        var priceCoreId = customSlotsChanger.GetItemFromString($"{modConfig.VariantCores.CarryCase.PriceCoreQuality} Quality Variant Core");
        var handbookPrice = priceCoreId == null ? modConfig.VariantCores.General.Price["Baseline"] * modConfig.VariantCores.CarryCase.PriceCoreAmount : modConfig.VariantCores.General.Price[modConfig.VariantCores.CarryCase.PriceCoreQuality] * modConfig.VariantCores.CarryCase.PriceCoreAmount;
        var variant = new VariantConfiguration
        {
            Description = "{CoreItem.CarryCase.Description}",
            ShortName = "{CoreItem.CarryCase.ShortName}",
            ItemTplToClone = "5e2af55f86f7746d4159f07c",
            HandbookPriceRoubles = handbookPrice,
            Rarity = "Unknown",
            VariantType = "Unknown",
            Barter = modConfig.VariantCores.CarryCase.Barter
        };
        if (priceCoreId is not null)
        {
            variant.Barter.BarterPrice.Add(priceCoreId.Id, 30);
        }
        else
        {
            variant.Barter.BarterPrice.Add("MONEY_ROUBLES", handbookPrice);
        }
        if (!modConfig.VariantCoresEnabled)
        {
            variant.Barter.LoyalLevel = 0;
        }

        var newGrids = new List<Grid>();
        var storageSizes = modConfig.VariantCores.CarryCase.GridTypesWidth;
        foreach (var (type, idList) in modDataStorage.CoresByType)
        {
            Grid columnCaseGrid = new()
            {
                Id = idDatabaseManager.GetCustomId($"VCCC:{type}:Grid:ID"),
                Name = $"VCCC:{type}:Grid",
                Parent = idDatabaseManager.GetCustomId($"Variant Core Carry Case:ID"),
                Prototype = "55d329c24bdc2d892f8b4567",
                Properties = new()
                {
                    CellsH = storageSizes[type],
                    CellsV = modConfig.VariantCores.CarryCase.GridHeight,
                    Filters = [new GridFilter { Filter = [.. idList] }],
                    IsSortingTable = false,
                    MaxCount = 0,
                    MaxWeight = 0,
                    MinCount = 0
                }
            };
            newGrids.Add(columnCaseGrid);
        }

        customLocales.RegisterTag("CarryCase.Grid.1", storageSizes["normal"].ToString());
        customLocales.RegisterTag("CarryCase.Grid.2", storageSizes["universal"].ToString());
        customLocales.RegisterTag("CarryCase.Grid.3", storageSizes["other"].ToString());
        var newId = itemGenerator.GenerateItem(
            $"Variant Core Carry Case",
            "{CoreItem.CarryCase.Name}",
            variant,
            "<color={rarity.Color}><b>{CoreItem.CarryCase.Name}</b></color>",
            "{CoreItem.CarryCase.Explanation}",
            new TemplateItemProperties
            {
                Width = modConfig.VariantCores.CarryCase.Properties.Width,
                Height = modConfig.VariantCores.CarryCase.Properties.Height,
                Grids = newGrids
            }
        );
        if (newId is null) { return; }
    }
    private void GenerateUnknownPackage()
    {
        var unknownPackageOverride = cloner.Clone(VariantCorePropertiesOverride);
        if (unknownPackageOverride is not null)
        {
            unknownPackageOverride.Ergonomics = 0;
            unknownPackageOverride.Width = modConfig.VariantCores.UnknownPackage.Properties.Width;
            unknownPackageOverride.Height = modConfig.VariantCores.UnknownPackage.Properties.Height;
            unknownPackageOverride.StackMaxSize = 1;
            unknownPackageOverride.Weight = modConfig.VariantCores.UnknownPackage.Properties.Weight;

            var totalWeight = UnknownPackageLootpool.Values.Sum();
            var formatted = UnknownPackageLootpool
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
            var barter = modConfig.VariantCores.UnknownPackage.Barter;
            if (modConfig.VariantCoresEnabled)
            {
                barter.BarterPrice.Add("MONEY_ROUBLES", modConfig.VariantCores.UnknownPackage.PriceInRoubles);
            }
            else
            {
                barter.LoyalLevel = 0;
            }
            customLocales.RegisterTag("UPackage.CoresReceived", modConfig.VariantCores.UnknownPackage.CoresReceived.ToString());
            customLocales.RegisterTag("UPackage.Lootpool", descText);
            var newId = itemGenerator.GenerateItem(
                $"Unknown Variant Weapon Core Package",
                "{CoreItem.UPackage.Name}",
                new VariantConfiguration
                {
                    Description = "{CoreItem.UPackage.Description}",
                    ShortName = "{CoreItem.UPackage.ShortName}",
                    ItemTplToClone = "6489b2b131a2135f0d7d0fcb",
                    HandbookPriceRoubles = modConfig.VariantCores.UnknownPackage.PriceInRoubles,
                    Barter = barter,
                    Rarity = "Unknown",
                    VariantType = "Unknown"
                },
                "<color={rarity.Color}><b>{CoreItem.UPackage.AddDesc}</b></color>",
                "{CoreItem.UPackage.Explanation}",
                unknownPackageOverride
            );
            if (newId is null) { return; }
            modDataStorage.AddCoreToStorage(newId, "Unknown", "other");
        }
    }
    public void CreateLootpoolForUnknownPackage()
    {
        if (idDatabaseManager.DbIds.TryGetValue($"Unknown Variant Weapon Core Package:ID", out var idDatabaseId))
        {
            (bool find, TemplateItem? item) = itemHelper.GetItem(idDatabaseId);
            if (find && item is not null)
            {
                inventoryConfig.RandomLootContainers.Add(idDatabaseId, new RewardDetails
                {
                    RewardCount = modConfig.VariantCores.UnknownPackage.CoresReceived,
                    FoundInRaid = false,
                    RewardTplPool = UnknownPackageLootpoolIds
                });
                // Fix found on MoxoPixel-Painter
                item.Name = idDatabaseId;
            }
        }
    }
}
