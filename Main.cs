using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Generators;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Integrations;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace DefinitiveWeaponVariants;

[Injectable(TypePriority = OnLoadOrder.Preload + 54000)]
public class DefinitiveWeaponVariants(
    ConfigData config,
    ConfigChecker configChecker,
    ModCheck compatibilityLayers,
    IdDatabaseManager idDatabaseManager,
    CustomItemCreator customItemCreator,
    CustomLootManager customLootManager,
    ItemGenerator itemGenerator,
    OtherItemsGenerator otherItemsGenerator,
    WeaponGenerator weaponGenerator,
    CustomLogger logger,
    APBSIntegration apbsIntegration,
    CustomLocales customLocales
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        configChecker.CheckConfig();

        compatibilityLayers.CheckMods();
        apbsIntegration.CheckModInstall();

        customLocales.Initialize();
        otherItemsGenerator.GenerateOtherItems();
        itemGenerator.GenerateAllItems();
        weaponGenerator.GenerateWeaponsFromVariantConfig();
        customLootManager.EditLoot();
        apbsIntegration.RunIntegration();
        idDatabaseManager.SaveDatabase();

        // Add 12.7x108mm B-32 to trader
        if (config.SpecialAmmoBuyableEnabled)
            customItemCreator.AddItemToTrader("5cde8864d7f00c0010373be1", config.DWVCaliberBarter);

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
