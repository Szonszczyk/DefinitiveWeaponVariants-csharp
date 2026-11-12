using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Generators;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Integrations;
using DefinitiveWeaponVariants.Models;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using System.Collections.Frozen;

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
    CustomTraderCreator customTraderCreator,
    RandomBoxesHelper randomBoxesHelper,
    ModDataStorage modDataStorage
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
        AmonyaIntegration.WeaponVariantIds = modDataStorage.WeaponVariantsInfo.Keys.ToFrozenSet(StringComparer.Ordinal);
        customLootManager.EditLoot();
        randomBoxesHelper.CreateLootpoolForBlindBoxes();
        idDatabaseManager.SaveDatabase();

        // Add 12.7x108mm B-32 to trader
        if (config.SpecialAmmoBuyableEnabled)
            customItemCreator.AddItemToTrader("5cde8864d7f00c0010373be1", config.DWVCaliberBarter);

        customLocales.RegisterLocales();
        logger.Ok($"Mod finished loading. Created {customItemCreator.ItemsAdded.Count} custom items!");

        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1002)]
public class DefinitiveWeaponVariantsFixBackgrounds(
    ModDataStorage modDataStorage,
    APBSIntegration apbsIntegration,
    WeaponGenerator weaponGenerator
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        weaponGenerator.AddVariantsToKillQuests();
        modDataStorage.FixBackgroundColors();
        await apbsIntegration.InitializeAsync(cancellationToken);
    }
}
