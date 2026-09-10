using DefinitiveWeaponVariants.Compatibility;
using DefinitiveWeaponVariants.CustomClasses;
using DefinitiveWeaponVariants.Generators;
using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Interfaces;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace DefinitiveWeaponVariants;

[Injectable(TypePriority = OnLoadOrder.Preload + 54000)]
public class DefinitiveWeaponVariants(
    ConfigData config,
    ConfigChecker configChecker,
    CompatibilityLayers compatibilityLayers,
    IdDatabaseManager idDatabaseManager,
    CustomItemCreator customItemCreator,
    CustomLootManager customLootManager,
    ItemGenerator itemGenerator,
    OtherItemsGenerator otherItemsGenerator,
    WeaponGenerator weaponGenerator,
    ModDataStorage modDataStorage,
    CustomLogger logger
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        configChecker.CheckConfig();
        modDataStorage.Initialize();

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
        if (config.SpecialAmmoBuyableEnabled)
            customItemCreator.AddItemToTrader("5cde8864d7f00c0010373be1", config.DWVCaliberBarter);

        logger.Ok($"Mod finished loading. Created {customItemCreator.ItemsAdded.Count} custom items!");

        return Task.CompletedTask;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostLoad + 102)]
public class DefinitiveWeaponVariantsFixBackgrounds(ModDataStorage modDataStorage) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        modDataStorage.FixBackgroundColors();
        return Task.CompletedTask;
    }
}
