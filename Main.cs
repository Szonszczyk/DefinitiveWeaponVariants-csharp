using DefinitiveWeaponVariants.Compatibility;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Generators;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 89000)]
public class DefinitiveWeaponVariants(
    ISptLogger<DefinitiveWeaponVariants> logger,
    IReadOnlyList<SptMod> modlist,
    CustomItemService customItemService,
    ModHelper modHelper,
    DatabaseService databaseService,
    ConfigServer configServer,
    JsonUtil jsonUtil,
    LocaleService localeService,
    ICloner cloner,
    ItemHelper itemHelper,
    RandomUtil randomUtil
) : IOnLoad
{
    public Task OnLoad()
    {
        ConfigLoader configLoader = new(logger, modHelper);
        ConfigChecker configChecker = new(logger, configLoader);
        CompatibilityLayers compatibilityLayers = new(logger, configLoader, modHelper, jsonUtil, databaseService, modlist);
        configChecker.CheckConfig();
        compatibilityLayers.CheckAllMods();

        ModDatabaseLoader modDatabaseLoader = new(logger, modHelper);
        IdDatabaseManager idDatabaseManager = new(logger, modHelper, jsonUtil);
        CustomItemCreator customItemCreator = new(logger, configServer, customItemService, databaseService, cloner);
        CustomPropertiesChanger customPropertiesChanger = new(logger);
        CustomSlotsChanger customSlotsChanger = new(logger, databaseService, modDatabaseLoader, cloner, idDatabaseManager);

        CustomLootManager customLootManager = new(
            logger,
            databaseService,
            cloner,
            configLoader,
            itemHelper,
            randomUtil,
            idDatabaseManager,
            configServer
        );
        ItemGenerator itemGenerator = new(
            logger,
            databaseService,
            modDatabaseLoader,
            idDatabaseManager,
            customItemCreator,
            customPropertiesChanger,
            customSlotsChanger,
            configLoader,
            cloner
        );

        itemGenerator.GenerateItems();
        

        WeaponGenerator weaponGenerator = new(
            logger,
            databaseService,
            modDatabaseLoader,
            idDatabaseManager,
            localeService,
            customItemCreator,
            customPropertiesChanger,
            customSlotsChanger,
            cloner,
            configLoader,
            itemHelper,
            customLootManager,
            compatibilityLayers
         );

        weaponGenerator.GenerateWeaponsFromVariantConfig();
        customLootManager.AddVariantsToLooseLoot();
        customLootManager.CreateLootpoolForBlindBoxes();
        customLootManager.AddCoresToBotPockets();
        compatibilityLayers.RunCompatibilityLayers();
        idDatabaseManager.SaveDatabase();

        // Add 12.7x108mm B-32 to trader
        if (configLoader.Config.SpecialAmmoBuyableEnabled)
            customItemCreator.AddItemToTrader("5cde8864d7f00c0010373be1", configLoader.Config.DWVCaliberBarter);

        logger.LogWithColor($"[{GetType().Namespace}] Mod finished loading. Created {customItemCreator.itemsLoaded} custom items!", LogTextColor.Green);

        return Task.CompletedTask;
    }
}