using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Generators;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
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
        ModDatabaseLoader modDatabaseLoader = new(logger, modHelper);
        IdDatabaseManager idDatabaseManager = new(logger, modHelper, jsonUtil);
        CustomItemCreator customItemCreator = new(logger, configServer, customItemService, databaseService);
        CustomPropertiesChanger customPropertiesChanger = new(logger);
        CustomSlotsChanger customSlotsChanger = new(logger, databaseService, modDatabaseLoader, cloner, idDatabaseManager);
        ItemGenerator itemGenerator = new(
            logger,
            databaseService,
            modDatabaseLoader,
            idDatabaseManager,
            customItemCreator,
            customPropertiesChanger,
            customSlotsChanger
        );
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
            randomUtil
         );


        itemGenerator.GenerateItems();
        weaponGenerator.GenerateWeaponsFromVariantConfig();
        idDatabaseManager.SaveDatabase();

        // Add 12.7x108mm B-32 to trader
        customItemCreator.AddItemToTrader("5cde8864d7f00c0010373be1", configLoader.Config.DWVCaliberBarter);

        logger.LogWithColor($"[{GetType().Namespace}] Mod finished loading. Created {customItemCreator.itemsLoaded} custom items!", LogTextColor.Green);

        return Task.CompletedTask;
    }
}