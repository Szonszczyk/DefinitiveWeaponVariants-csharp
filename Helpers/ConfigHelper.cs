using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.Loaders;
using DefinitiveWeaponVariants.Models;
using SPTarkov.DI.Annotations;

namespace DefinitiveWeaponVariants.Helpers;

[Injectable(InjectionType.Singleton)]
public class ConfigHelper(
    CustomLogger logger,
    ConfigData config,
    ModDatabaseLoader modDatabaseLoader
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

        CheckDictionaryStringInt(config.VariantCores.General.Price);

        CheckDictionaryStringInt(config.APBSTierConfig);

        config.StaticLootProbability = CheckProbability(config.StaticLootProbability, nameof(config.StaticLootProbability));
        config.MarkedRoomsProbability = CheckProbability(config.MarkedRoomsProbability, nameof(config.MarkedRoomsProbability));

        var traderDynamicAssortConfig = config.Trader.Traders.FirstOrDefault();
        traderDynamicAssortConfig?.TraderId = modDatabaseLoader.TraderBase.Id;
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
    private void CheckDictionaryStringInt(Dictionary<string, int> dict)
    {
        foreach (var q in RaritySettings.RarityList())
        {
            if (!dict.TryGetValue(q, out int value))
                dict.Add(q, 0);
            else if (value < 0) dict[q] = 0;
            if (!config.Generate[q]) dict[q] = 0;
        }
    }
    private float CheckProbability(float value, string name)
    {
        var clamped = Math.Clamp(value, 0f, 0.99f);
        if (value != clamped) logger.Error($"Config option for '{name}' is incorrect. Is {value}, should be between 0.99 and 0.");
        return clamped;
    }
}
