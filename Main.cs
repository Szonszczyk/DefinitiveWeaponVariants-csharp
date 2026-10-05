using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Generators;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Integrations;
using DefinitiveWeaponVariants.Models;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace DefinitiveWeaponVariants;

[Injectable(TypePriority = OnLoadOrder.Preload + 54000)]
public class DefinitiveWeaponVariants(
    ConfigData config,
    ConfigHelper configHelper,
    IdDatabaseManager idDatabaseManager,
    CustomItemCreator customItemCreator,
    CustomLootManager customLootManager,
    ItemGenerator itemGenerator,
    OtherItemsGenerator otherItemsGenerator,
    WeaponGenerator weaponGenerator,
    CustomLogger logger,
    APBSIntegration apbsIntegration,
    CustomLocales customLocales,
    CustomTraderCreator customTraderCreator
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        configHelper.CheckConfig();

        apbsIntegration.CheckModInstall();

        customLocales.Initialize();
        customTraderCreator.Initialize();
        otherItemsGenerator.GenerateOtherItems();
        itemGenerator.GenerateAllItems();
        weaponGenerator.GenerateWeaponsFromVariantConfig();
        customLootManager.EditLoot();
        apbsIntegration.RunIntegration();
        idDatabaseManager.SaveDatabase();

        // Add 12.7x108mm B-32 to trader
        if (config.SpecialAmmoBuyableEnabled)
            customItemCreator.AddItemToTrader("ee840a5ba014e9c5478e2137", config.DWVCaliberBarter);

        customLocales.RegisterLocales();
        logger.Ok($"Mod finished loading. Created {customItemCreator.ItemsAdded.Count} custom items!");

        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostLoad + 102)]
public class DefinitiveWeaponVariantsFixBackgrounds(
    ModDataStorage modDataStorage,
    APBSIntegration apbsIntegration
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        modDataStorage.FixBackgroundColors();
        apbsIntegration.RefreshBlacklist(cancellationToken);
        return Task.CompletedTask;
    }
}
