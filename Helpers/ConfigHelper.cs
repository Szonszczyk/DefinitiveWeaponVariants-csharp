using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.Models;
using SPTarkov.DI.Annotations;

namespace DefinitiveWeaponVariants.Helpers;

[Injectable(InjectionType.Singleton)]
public class ConfigHelper(
    CustomLogger logger,
    ConfigData config
)
{

    // Add any missing values, change incorrect ones + false/zero all values if Generate is false for this quality
    public void CheckConfig()
    {
        CheckDictionaryStringBool(config.Generate);
        CheckDictionaryStringBool(config.Airdrop);
        CheckDictionaryStringBool(config.Fence);
        CheckDictionaryStringBool(config.Flea);
        CheckDictionaryStringBool(config.Marked);
        CheckDictionaryStringBool(config.StaticLoot);

        CheckDictionaryStringInt(config.QualityWeights);

        CheckDictionaryStringInt(config.VariantCoresPrice, respectGeneration: false);

        CheckDictionaryStringInt(config.APBSTierConfig);

        if (config.APBSWeaponsAvailable <= 0)
        {
            logger.Warning("APBSWeaponsAvailable must be positive; using 75. Disable EnableAPBSWeaponRotation to remove temporary restrictions.");
            config.APBSWeaponsAvailable = 75;
        }
        if (config.APBSWeaponRotationIntervalMinutes <= 0)
        {
            logger.Warning("APBSWeaponRotationIntervalMinutes must be positive; using 60 minutes.");
            config.APBSWeaponRotationIntervalMinutes = 60;
        }

        config.StaticLootProbability = CheckProbability(config.StaticLootProbability, nameof(config.StaticLootProbability));
        config.MarkedRoomsProbability = CheckProbability(config.MarkedRoomsProbability, nameof(config.MarkedRoomsProbability));
        config.UnknownPackageProbability = CheckProbability(config.UnknownPackageProbability, nameof(config.UnknownPackageProbability));
        foreach (var bot in config.VCNormalFoundOnEnemies.Keys.ToArray())
            config.VCNormalFoundOnEnemies[bot] = CheckProbability(config.VCNormalFoundOnEnemies[bot], $"VCNormalFoundOnEnemies.{bot}");

        config.TraderDynamicOfferCount = CheckInt(config.TraderDynamicOfferCount, 0, 50, nameof(config.TraderDynamicOfferCount));
        config.TraderRefreshSeconds.Min = CheckInt(config.TraderRefreshSeconds.Min, 30, 7200, "TraderRefreshSeconds.Min");
        config.TraderRefreshSeconds.Max = CheckInt(config.TraderRefreshSeconds.Max, 60, 14400, "TraderRefreshSeconds.Max");
        if (config.TraderRefreshSeconds.Max < config.TraderRefreshSeconds.Min)
        {
            logger.Warning("TraderRefreshSeconds.Max must be at least Min; using Min for both.");
            config.TraderRefreshSeconds.Max = config.TraderRefreshSeconds.Min;
        }
        config.VCUniversalToUniqueRatio = CheckInt(config.VCUniversalToUniqueRatio, 1, 100, nameof(config.VCUniversalToUniqueRatio));
        config.VCNormalUpgradeRatio = CheckInt(config.VCNormalUpgradeRatio, 2, 10, nameof(config.VCNormalUpgradeRatio));
        config.VCLockedUpgradeRatio = CheckInt(config.VCLockedUpgradeRatio, 1, int.MaxValue, nameof(config.VCLockedUpgradeRatio));
        config.UnknownPackageCoresReceived = CheckInt(config.UnknownPackageCoresReceived, 1, 100, nameof(config.UnknownPackageCoresReceived));
        config.BlindBoxesCoresPrice = CheckInt(config.BlindBoxesCoresPrice, 1, 100, nameof(config.BlindBoxesCoresPrice));
        config.ExpCaseHandbookPriceRoubles = CheckInt(config.ExpCaseHandbookPriceRoubles, 0, int.MaxValue, nameof(config.ExpCaseHandbookPriceRoubles));
        config.CarryCaseHandbookPriceRoubles = CheckInt(config.CarryCaseHandbookPriceRoubles, 0, int.MaxValue, nameof(config.CarryCaseHandbookPriceRoubles));
        if (!RaritySettings.RarityList().Contains(config.VCNormalUpgradeUpToQuality))
        {
            logger.Warning("VCNormalUpgradeUpToQuality is invalid; using Baseline.");
            config.VCNormalUpgradeUpToQuality = "Baseline";
        }
    }
    
    private void CheckDictionaryStringBool(Dictionary<string, bool> dict)
    {
        foreach (var q in RaritySettings.RarityList())
        {
            if (!dict.TryGetValue(q, out bool _))
                dict.Add(q, false);
            if (!config.Generate[q]) dict[q] = false;
        }
    }
    private void CheckDictionaryStringInt(Dictionary<string, int> dict, bool respectGeneration = true)
    {
        foreach (var q in RaritySettings.RarityList())
        {
            if (!dict.TryGetValue(q, out int value))
                dict.Add(q, 0);
            else if (value < 0) dict[q] = 0;
            if (respectGeneration && !config.Generate[q]) dict[q] = 0;
        }
    }
    private float CheckProbability(float value, string name)
    {
        var clamped = float.IsFinite(value) ? Math.Clamp(value, 0f, 0.99f) : 0f;
        if (value != clamped) logger.Warning($"Config option '{name}' is {value}; using {clamped}. Expected a value between 0 and 0.99.");
        return clamped;
    }

    private int CheckInt(int value, int minimum, int maximum, string name)
    {
        var clamped = Math.Clamp(value, minimum, maximum);
        if (value != clamped) logger.Warning($"Config option '{name}' is {value}; using {clamped}. Expected {minimum}-{maximum}.");
        return clamped;
    }
}
