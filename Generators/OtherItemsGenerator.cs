using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Loaders;
using DefinitiveWeaponVariants.Models;
using MudBlazor;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.Generators;

[Injectable(InjectionType.Singleton)]
public class OtherItemsGenerator(
    IdDatabaseManager idDatabaseManager,
    CustomSlotsChanger customSlotsChanger,
    ConfigData modConfig,
    ICloner cloner,
    ModDataStorage modDataStorage,
    ItemGenerator itemGenerator,
    CustomItemCreator customItemCreator,
    CustomLocales customLocales,
    UCoresGenerator uCoresGenerator,
    RandomBoxesHelper randomBoxesHelper
)
{
    private TemplateItemProperties VariantCorePropertiesOverride { get; } = new()
    {
        StackMaxSize = 10,
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
        uCoresGenerator.GenerateUniversalCores();
        GenerateUnknownPackage();
        GenerateCoreHolder();
        GenerateCoreCase();

        randomBoxesHelper.AddUnknownPackageLootpool();
    }

    private void GenerateNormalCores()
    {
        var corePropertiesOverride = cloner.Clone(VariantCorePropertiesOverride);
        if (corePropertiesOverride is null) return;
        corePropertiesOverride.Ergonomics = modConfig.VCNormalErgonomics;
        foreach (var (quality, enabled) in modConfig.Generate)
        {
            // Generate normal core
            var variant = new VariantConfiguration
            {
                Description = "{CoreItem.Normal.Description}",
                ShortName = $"{{Quality.{quality}.ShortName}} {{CoreItem.Normal.ShortName}}",
                ItemTplToClone = "58d2912286f7744e27117493",
                HandbookPriceRoubles = modConfig.VariantCoresPrice[quality] * modConfig.VCNormalPriceMultiplier,
                Rarity = quality,
                VariantType = quality,
                Changes = new() { Parent = "5448fe124bdc2da5018b4567" }
            };
            if (enabled && modConfig.VCNormalBuyable)
            {
                variant.Barter = cloner.Clone(modConfig.VCNormalBarter);
                variant.Barter!.BarterPrice = new() { ["MONEY_ROUBLES"] = modConfig.VariantCoresPrice[quality] * modConfig.VCNormalPriceMultiplier };
            }
            var newId = itemGenerator.GenerateItem(
                $"{quality} Quality Variant Core",
                $"{{Quality.{quality}.Name}} {{CoreItem.Normal.Name}}",
                variant,
                $"<color={{rarity.Color}}><b>{{Quality.{quality}.Name}} {{CoreItem.Normal.Name}}</b></color>",
                $"{{CoreItem.Normal.Explanation}}.{(modConfig.VCNormalFoundOnEnemies.TryGetValue("assault", out _) ? "\n{CoreItem.Normal.AddDesc}" : "")}",
                corePropertiesOverride
            );
            if (newId is null) { continue; }
            modDataStorage.CoreHandbookPrices[newId] = variant.HandbookPriceRoubles!.Value;
            modDataStorage.CoreIDsByQuality.Add(quality, newId);
            modDataStorage.AddCoreToStorage(newId, quality, "normal");
            var rarity = RaritySettings.GetByName(variant.Rarity);
            modDataStorage.ColorsByItems.Add(newId, [rarity.Color, "#FFFFFF"]);
            randomBoxesHelper.UnknownPackageDisplayItems.Add(
                newId,
                new()
                {
                    Description = $"<color={rarity.Color}><b>{{Quality.{quality}.Name}} {{CoreItem.Normal.Name}} (90%/10% {{CoreItem.Locked.ShortName}})</b></color>",
                    Probability = modConfig.QualityWeights[quality] * 10,
                    Quality = quality
                }
            );
            randomBoxesHelper.UnknownPackageItems.Add(
                newId,
                new()
                {
                    Probability = modConfig.QualityWeights[quality] * 9,
                    Quality = quality
                }
            );
        }
        if (modConfig.VCNormalUpgradable)
        {
            var qualities = RaritySettings.RarityList();
            var maximumQuality = qualities.IndexOf(modConfig.VCNormalUpgradeUpToQuality);
            for (var i = 1; i <= maximumQuality; i++)
            {
                var qualityNow = qualities[i];
                var qualityPrev = qualities[i - 1];
                modConfig.Generate.TryGetValue(qualityNow, out var qualityNowEnabled);
                modConfig.Generate.TryGetValue(qualityPrev, out var qualityPrevEnabled);
                if (!qualityNowEnabled || !qualityPrevEnabled) { continue; }
                modDataStorage.CoreIDsByQuality.TryGetValue(qualityNow, out var nowCore);
                modDataStorage.CoreIDsByQuality.TryGetValue(qualityPrev, out var prevCore);
                var barterConfig = cloner.Clone(modConfig.VCNormalUpgradeBarter);
                if (barterConfig == null || nowCore == null || prevCore == null) continue;
                barterConfig.BarterPrice.Add(prevCore, modConfig.VCNormalUpgradeRatio);
                customItemCreator.AddItemToTrader(nowCore, barterConfig);
            }
        }
    }

    private void GenerateLockedCores()
    {
        var corePropertiesOverride = cloner.Clone(VariantCorePropertiesOverride);
        if (corePropertiesOverride is null) return;
        corePropertiesOverride.Ergonomics = modConfig.VCLockedErgonomics;
        corePropertiesOverride.Accuracy = modConfig.VCLockedAccuracy;
        foreach (var (quality, enabled) in modConfig.Generate)
        {
            var variant = new VariantConfiguration
            {
                Description = "{CoreItem.Locked.Description}",
                ShortName = $"{{Quality.{quality}.ShortName}} {{CoreItem.Locked.ShortName}}",
                ItemTplToClone = "58d2912286f7744e27117493",
                HandbookPriceRoubles = modConfig.VariantCoresPrice[quality] * modConfig.VCLockedPriceMultiplier,
                Rarity = quality,
                VariantType = quality,
                Changes = new() { Parent = "5448fe124bdc2da5018b4567" }
            };
            var normalCoreId = idDatabaseManager.GetCustomId($"{quality} Quality Variant Core:ID");
            if (enabled && modConfig.VCLockedUpgradeBarter.LoyalLevel > 0)
            {
                variant.Barter = cloner.Clone(modConfig.VCLockedUpgradeBarter);
                variant.Barter!.BarterPrice = new() { [normalCoreId] = modConfig.VCLockedUpgradeRatio };
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
            modDataStorage.CoreHandbookPrices[newId] = variant.HandbookPriceRoubles!.Value;
            modDataStorage.AddCoreToStorage(newId, quality, "normal");
            var rarity = RaritySettings.GetByName(variant.Rarity);
            modDataStorage.ColorsByItems.Add(newId, [rarity.Color, "#000000"]);
            randomBoxesHelper.UnknownPackageItems.Add(
                newId,
                new()
                {
                    Probability = modConfig.QualityWeights[quality] * 1,
                    Quality = quality
                }
            );
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
                HandbookPriceRoubles = modConfig.VariantCoresPrice[quality] * modConfig.BlindBoxesPriceMultiplier,
                Rarity = quality,
                VariantType = quality
            };
            var coreId = customSlotsChanger.GetItemFromString($"{quality} Quality Variant Core")?.Id ?? "5449016a4bdc2d6f028b456f";
            if (enabled && modConfig.BlindBoxesEnabled)
            {
                var barter = cloner.Clone(modConfig.BlindBoxesBarter);
                barter ??= new CustomBarterConfig();
                barter.BarterPrice = new() { [coreId] = modConfig.BlindBoxesCoresPrice };
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
                    Width = modConfig.BlindBoxesProperties.Width,
                    Height = modConfig.BlindBoxesProperties.Height,
                    Weight = modConfig.BlindBoxesProperties.Weight
                }
            );
            if (newId is null) { continue; }
            modDataStorage.AddCoreToStorage(newId, quality, "other");
            randomBoxesHelper.BlindBoxPerQuality.Add(quality, newId);
            var rarity = RaritySettings.GetByName(variant.Rarity);
            modDataStorage.ColorsByItems.Add(newId, [rarity.Color]);
        }
    }
    private void GenerateCoreHolder()
    {
        var handbookPrice = GetHandbookPrice(modConfig.ExpCaseBarter, modConfig.ExpCaseHandbookPriceRoubles);
        var variant = new VariantConfiguration
        {
            Description = "{CoreItem.ExpCase.Description}",
            ShortName = "{CoreItem.ExpCase.ShortName}",
            ItemTplToClone = "5e2af55f86f7746d4159f07c",
            HandbookPriceRoubles = handbookPrice,
            Rarity = "Unknown",
            VariantType = "Unknown",
            Barter = cloner.Clone(modConfig.ExpCaseBarter)
        };

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
                    CellsH = modConfig.ExpCaseGridProperties.CellsH,
                    CellsV = modConfig.ExpCaseGridProperties.CellsV,
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
                Width = modConfig.ExpCaseProperties.Width,
                Height = modConfig.ExpCaseProperties.Height,
                Grids = newGrids,
                Weight = modConfig.ExpCaseProperties.Weight
            }
        );
        if (newId is not null)
        {
            customItemCreator.AddItemToSecureContainer(newId);
            var rarity = RaritySettings.GetByName(variant.Rarity);
            modDataStorage.ColorsByItems.Add(newId, [rarity.Color]);
        }
    }

    private void GenerateCoreCase()
    {
        var handbookPrice = GetHandbookPrice(modConfig.CarryCaseBarter, modConfig.CarryCaseHandbookPriceRoubles);
        var variant = new VariantConfiguration
        {
            Description = "{CoreItem.CarryCase.Description}",
            ShortName = "{CoreItem.CarryCase.ShortName}",
            ItemTplToClone = "5e2af55f86f7746d4159f07c",
            HandbookPriceRoubles = handbookPrice,
            Rarity = "Unknown",
            VariantType = "Unknown",
            Barter = cloner.Clone(modConfig.CarryCaseBarter)
        };

        var newGrids = new List<Grid>();
        var storageSizes = modConfig.CarryCaseGridTypesWidth;
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
                    CellsV = modConfig.CarryCaseGridHeight,
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
                Width = modConfig.CarryCaseProperties.Width,
                Height = modConfig.CarryCaseProperties.Height,
                Grids = newGrids
            }
        );
        if (newId is null) { return; }
        var rarity = RaritySettings.GetByName(variant.Rarity);
        modDataStorage.ColorsByItems.Add(newId, [rarity.Color]);
    }
    private void GenerateUnknownPackage()
    {
        var unknownPackageOverride = cloner.Clone(VariantCorePropertiesOverride);
        if (unknownPackageOverride is not null)
        {
            unknownPackageOverride.Ergonomics = 0;
            unknownPackageOverride.Width = modConfig.UnknownPackageProperties.Width;
            unknownPackageOverride.Height = modConfig.UnknownPackageProperties.Height;
            unknownPackageOverride.StackMaxSize = 1;
            unknownPackageOverride.Weight = modConfig.UnknownPackageProperties.Weight;

            var barter = cloner.Clone(modConfig.UnknownPackageBarter)!;
            if (!modConfig.UnknownPackageEnabled) barter.LoyalLevel = 0;
            customLocales.RegisterTag("UPackage.CoresReceived", modConfig.UnknownPackageCoresReceived.ToString());
            customLocales.RegisterTag("UPackage.Lootpool", randomBoxesHelper.CreateUnknownPackageLootpool());
            var newId = itemGenerator.GenerateItem(
                $"Unknown Variant Weapon Core Package",
                "{CoreItem.UPackage.Name}",
                new VariantConfiguration
                {
                    Description = "{CoreItem.UPackage.Description}",
                    ShortName = "{CoreItem.UPackage.ShortName}",
                    ItemTplToClone = "6489b2b131a2135f0d7d0fcb",
                    HandbookPriceRoubles = GetHandbookPrice(barter, 250000),
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
            randomBoxesHelper.UnknownPackageId = newId;
            var rarity = RaritySettings.GetByName("Unknown");
            modDataStorage.ColorsByItems.Add(newId, [rarity.Color]);
        }
    }

    private double GetHandbookPrice(CustomBarterConfig barter, int fallback)
        => HandbookPriceCalculator.Calculate(barter.BarterPrice, fallback, key =>
        {
            var id = customSlotsChanger.GetItemFromString(key)?.Id;
            return id is not null && modDataStorage.CoreHandbookPrices.TryGetValue(id, out var price)
                ? price : null;
        });
}
