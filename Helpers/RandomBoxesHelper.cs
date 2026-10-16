using DefinitiveWeaponVariants.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace DefinitiveWeaponVariants.Helpers;

[Injectable(InjectionType.Singleton)]
public class RandomBoxesHelper(
    ModDataStorage modDataStorage,
    InventoryConfig inventoryConfig,
    ItemHelper itemHelper,
    ConfigData modConfig
    )
{
    public Dictionary<string, string> BlindBoxPerQuality { get; private set; } = [];
    public void CreateLootpoolForBlindBoxes()
    {
        foreach (var (quality, boxId) in BlindBoxPerQuality)
        {
            if (!modDataStorage.VariantPresets.TryGetValue(quality, out var allWeaponsInData)) continue;
            Dictionary<MongoId, double> weapons = [];
            foreach (var weapon in allWeaponsInData)
            {
                var rootItem = weapon.First();
                weapons.Add(rootItem.Template, 1);
            }
            (bool find, TemplateItem? item) = itemHelper.GetItem(boxId);
            if (find && item is not null)
            {
                inventoryConfig.RandomLootContainers.Add(boxId, new RewardDetails
                {
                    RewardCount = 1,
                    FoundInRaid = false,
                    RewardTplPool = weapons
                });
                // Fix found on MoxoPixel-Painter
                item.Name = boxId;
            }
        }
    }

    public string UnknownPackageId { get; set; } = string.Empty;
    public class UnknownPackageItemEntry
    {
        public string Description { get; set; } = string.Empty;
        public int Probability { get; set; } = 0;
        public string Quality { get; set; } = string.Empty;
    }
    public Dictionary<string, UnknownPackageItemEntry> UnknownPackageDisplayItems { get; set; } = [];
    public Dictionary<string, UnknownPackageItemEntry> UnknownPackageItems { get; set; } = [];
    private Dictionary<MongoId, double> UnknownPackageLootpool { get; set; } = [];

    public string CreateUnknownPackageLootpool()
    {
        if (UnknownPackageItems.Count == 0) return string.Empty;

        // Universal cores probability is tied to Unique core probability 1:1,
        // but can be multiplied by config to 2:1 and so on
        var universalProbability = UnknownPackageItems.Values
            .Where(item => item.Quality == "universal")
            .Sum(item => item.Probability);
        var uniqueProbability = UnknownPackageItems.Values
            .Where(item => item.Quality == "Unique")
            .Sum(item => item.Probability);
        int universalMultiplier = 1;
        int otherMultiplier = 1;
        if (universalProbability > 0 && uniqueProbability > 0)
        {
            var a = universalProbability;
            var b = uniqueProbability;

            // Greatest common divisor.
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }
            universalMultiplier = uniqueProbability / a;
            otherMultiplier = universalProbability / a;
        }
        // Refresh probability
        foreach (var (id, itemToAdd) in UnknownPackageItems)
        {
            var multiplier = itemToAdd.Quality == "universal" ? universalMultiplier /* * configMultiplier */ : otherMultiplier;
            if (itemToAdd.Probability == 0) continue;
            UnknownPackageLootpool.Add(id, itemToAdd.Probability * multiplier);
        }
        var unknownPackageLootpoolDisplay = new Dictionary<string, double>();
        foreach (var (id, itemToAdd) in UnknownPackageDisplayItems)
        {
            var multiplier = itemToAdd.Quality == "universal" ? universalMultiplier /* * configMultiplier */ : otherMultiplier;
            if (itemToAdd.Probability == 0) continue;
            unknownPackageLootpoolDisplay.Add(itemToAdd.Description, itemToAdd.Probability * multiplier);
        }

        var totalWeight = UnknownPackageLootpool.Values.Sum();
        var formatted = unknownPackageLootpoolDisplay
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
        return descText;
    }
    public void AddUnknownPackageLootpool()
    {
        if (UnknownPackageId == string.Empty || UnknownPackageLootpool.Count == 0) return;

        (bool find, TemplateItem? item) = itemHelper.GetItem(UnknownPackageId);
        if (find && item is not null)
        {
            inventoryConfig.RandomLootContainers.Add(UnknownPackageId, new RewardDetails
            {
                RewardCount = modConfig.VariantCores.UnknownPackage.CoresReceived,
                FoundInRaid = false,
                RewardTplPool = UnknownPackageLootpool
            });
            // Fix found on MoxoPixel-Painter
            item.Name = UnknownPackageId;
        }
    }
}