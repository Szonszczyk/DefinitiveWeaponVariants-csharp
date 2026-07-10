using DefinitiveWeaponVariants.Constants;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;

namespace DefinitiveWeaponVariants.Helpers;

[Injectable(InjectionType.Singleton)]
public class ConfigChecker(
    ISptLogger<DefinitiveWeaponVariants> logger,
    ConfigLoader configLoader
)
{
    private readonly ConfigData modConfig = configLoader.Config;

    // Add any missing values, change incorrect ones + false/zero all values if Generate is false for this quality
    public void CheckConfig()
    {
        CheckDictionaryStringBool(modConfig.Generate);
        CheckDictionaryStringBool(modConfig.Airdrop);
        CheckDictionaryStringBool(modConfig.Fence);
        CheckDictionaryStringBool(modConfig.Flea);
        CheckDictionaryStringBool(modConfig.Marked);
        CheckDictionaryStringBool(modConfig.StaticLoot);

        CheckDictionaryStringInt(modConfig.QualityWeights);

        CheckDictionaryStringInt(modConfig.VariantCores.General.Price);

        CheckDictionaryStringInt(modConfig.APBSTierConfig);

        modConfig.StaticLootProbability = CheckProbability(modConfig.StaticLootProbability, nameof(modConfig.StaticLootProbability));
        modConfig.MarkedRoomsProbability = CheckProbability(modConfig.MarkedRoomsProbability, nameof(modConfig.MarkedRoomsProbability));
    }
    
    private void CheckDictionaryStringBool(Dictionary<string, bool> dict)
    {
        foreach (var q in RaritySettings.RarityList())
        {
            if (!dict.TryGetValue(q, out bool _))
                dict.Add(q, false);
            if (!modConfig.Generate[q]) dict[q] = false;
        }
    }
    private void CheckDictionaryStringInt(Dictionary<string, int> dict)
    {
        foreach (var q in RaritySettings.RarityList())
        {
            if (!dict.TryGetValue(q, out int value))
                dict.Add(q, 0);
            else if (value < 0) dict[q] = 0;
            if (!modConfig.Generate[q]) dict[q] = 0;
        }
    }
    private float CheckProbability(float value, string name)
    {
        var clamped = Math.Clamp(value, 0f, 0.99f);
        if (value != clamped) logger.LogWithColor($"[{GetType().Namespace}] Config option for '{name}' is incorrect. Is {value}, should be between 0.99 and 0.", LogTextColor.Red);
        return clamped;
    }
}
