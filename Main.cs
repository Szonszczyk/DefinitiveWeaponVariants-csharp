using DefinitiveWeaponVariants.Compatibility;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Generators;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;


namespace DefinitiveWeaponVariants;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 89000)]
public class DefinitiveWeaponVariants(
    ISptLogger<DefinitiveWeaponVariants> logger,
    DatabaseService databaseService,
    ConfigServer configServer,
    LocaleService localeService,
    ConfigLoader configLoader,
    ConfigChecker configChecker,
    CompatibilityLayers compatibilityLayers,
    IdDatabaseManager idDatabaseManager,
    CustomItemCreator customItemCreator,
    CustomLootManager customLootManager,
    ItemGenerator itemGenerator,
    OtherItemsGenerator otherItemsGenerator,
    WeaponGenerator weaponGenerator,
    ModDataStorage modDataStorage
) : IOnLoad
{
    public Task OnLoad()
    {
        configChecker.CheckConfig();
        modDataStorage.Initialize(databaseService, configServer, localeService);

        compatibilityLayers.CheckMods();

        otherItemsGenerator.GenerateOtherItems();
        itemGenerator.GenerateAllItems();
        weaponGenerator.GenerateWeaponsFromVariantConfig();
        customLootManager.AddVariantsToLooseLoot();
        customLootManager.CreateLootpoolForBlindBoxes();
        customLootManager.AddCoresToBotPockets();
        compatibilityLayers.RunCompatibilityLayers();
        idDatabaseManager.SaveDatabase();

        // Add 12.7x108mm B-32 to trader
        if (configLoader.Config.SpecialAmmoBuyableEnabled)
            customItemCreator.AddItemToTrader("5cde8864d7f00c0010373be1", configLoader.Config.DWVCaliberBarter);

        logger.LogWithColor($"[{GetType().Namespace}] Mod finished loading. Created {customItemCreator.ItemsAdded.Count} custom items!", LogTextColor.Green);

        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 102)]
public class DefinitiveWeaponVariantsFixBackgrounds(ModDataStorage modDataStorage) : IOnLoad
{
    public Task OnLoad()
    {
        modDataStorage.FixBackgroundColors();
        return Task.CompletedTask;
    }
}