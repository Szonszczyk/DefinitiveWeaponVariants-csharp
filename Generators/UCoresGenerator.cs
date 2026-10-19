using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Loaders;
using DefinitiveWeaponVariants.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.Generators;

[Injectable(InjectionType.Singleton)]
public class UCoresGenerator(
    ConfigData config,
    ICloner cloner,
    ModDatabaseLoader modDatabaseLoader,
    ItemGenerator itemGenerator,
    RandomBoxesHelper randomBoxesHelper,
    ModDataStorage modDataStorage
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

    public void GenerateUniversalCores()
    {
        foreach(var (themeName, themeConfig) in modDatabaseLoader.DbCores)
        {
            foreach (var (internalName, coreConfig) in themeConfig.Items)
            {
                var coreProperties = cloner.Clone(VariantCorePropertiesOverride);
                if (coreProperties is null) continue;
                MergeTemplateItemProperties(coreProperties, coreConfig.Properties);

                var newId = itemGenerator.GenerateItem(
                    $"UCore_{internalName}",
                    $"<b><dwv-gradient={themeConfig.Theme.Color1},{themeConfig.Theme.Color2}>{{UCore.{internalName}.Name}} {{UCore.Name}}</dwv-gradient></b>",
                    new VariantConfiguration
                    {
                        Description = "{UCore.Description}",
                        ShortName = $"{{UCore.{internalName}.Name}}",
                        ItemTplToClone = "58d2912286f7744e27117493",
                        HandbookPriceRoubles = config.VariantCoresPrice["Unique"] * 5,
                        Rarity = "Unknown",
                        VariantType = "Unknown"
                    },
                    $"<b><dwv-gradient={themeConfig.Theme.Color1},{themeConfig.Theme.Color2}>{{UCore.{themeName}Theme.Name}} {{UCore.FullName}}: {{UCore.{internalName}.Name}}</dwv-gradient></b>",
                    "{UCore.Explanation}",
                    coreProperties
                );
                if (newId is null) { continue; }
                modDataStorage.CoreHandbookPrices[newId] = config.VariantCoresPrice["Unique"] * 5;

                modDataStorage.AddCoreToStorage(newId, "Unknown", "universal");
                randomBoxesHelper.UnknownPackageItems.Add(newId, new() { Probability = 1, Quality = "universal" });
            }
            randomBoxesHelper.UnknownPackageDisplayItems.Add(
                $"universal_{themeName}",
                new()
                {
                    Description = $"<b><dwv-gradient={themeConfig.Theme.Color1},{themeConfig.Theme.Color2}>{{UCore.FullName}} | {{UCore.{themeName}Theme.Name}}</dwv-gradient></b>",
                    Probability = themeConfig.Items.Count,
                    Quality = "universal"
                }
            );
        }
    }

    private static void MergeTemplateItemProperties(
        TemplateItemProperties target,
        TemplateItemProperties overrides
    )
    {
        foreach (var property in typeof(TemplateItemProperties).GetProperties())
        {
            if (!property.CanRead || !property.CanWrite)
                continue;

            var value = property.GetValue(overrides);
            if (value is not null)
                property.SetValue(target, value);
        }
    }
}
