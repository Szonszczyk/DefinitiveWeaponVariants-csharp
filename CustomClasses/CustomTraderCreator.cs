using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Loaders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;
using Path = System.IO.Path;

namespace DefinitiveWeaponVariants.CustomClasses;

[Injectable(InjectionType.Singleton)]
public class CustomTraderCreator(
    CustomLogger logger,
    ICloner cloner,
    ImageRouter imageRouter,
    ModHelper modHelper,
    ModDatabaseLoader modDatabaseLoader,
    TimeUtil timeUtil,
    RagfairConfig ragfairConfig,
    TraderConfig traderConfig,
    TradersTable tradersTable,
    CustomLocales customLocales
)
{
    public List<string> questImages { get; set; } = [];
    public void Initialize()
    {
        RegisterTraderImage();
        SetTraderUpdateTime();
        ragfairConfig.Traders.TryAdd(modDatabaseLoader.TraderBase.Id, true);
        AddTraderWithEmptyAssortToDb();
        AddTraderToLocales(modDatabaseLoader.TraderBase);
        RegisterQuestImages();
    }
    private void RegisterTraderImage()
    {
        var baseJson = modDatabaseLoader.TraderBase;
        if (baseJson.Avatar is null) return;
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());

        var traderImagePath = Path.Combine(pathToMod, "res", "Trader.png");
        imageRouter.AddRoute(baseJson.Avatar.Replace(".jpg", ""), traderImagePath);
    }
    private void SetTraderUpdateTime()
    {
        var refreshTimeSecondsMin = timeUtil.GetMinutesAsSeconds(1);
        var refreshTimeSecondsMax = timeUtil.GetMinutesAsSeconds(2);
        var baseJson = modDatabaseLoader.TraderBase;
        // Add refresh time in seconds to config
        var traderRefreshRecord = new UpdateTime
        {
            TraderId = baseJson.Id,
            Seconds = new MinMax<int>((int)refreshTimeSecondsMin, (int)refreshTimeSecondsMax)
        };

        traderConfig.UpdateTime.Add(traderRefreshRecord);
    }
    private void AddTraderWithEmptyAssortToDb()
    {
        var traderDetailsToAdd = modDatabaseLoader.TraderBase;
        var emptyTraderItemAssortObject = new TraderAssort
        {
            Items = [],
            BarterScheme = [],
            LoyalLevelItems = []
        };
        var traderBase = cloner.Clone(traderDetailsToAdd);
        if (traderBase == null) return;
        var traderDataToAdd = new Trader
        {
            Assort = emptyTraderItemAssortObject,
            Base = traderBase,
            QuestAssort = new()
            {
                { "started", new() },
                { "success", new() },
                { "fail", new() }
            },
            Dialogue = []
        };

        if (!tradersTable.TryAdd(traderDetailsToAdd.Id, traderDataToAdd))
        {
            logger.Error($" Failed to add Trader to databases!");
        }
    }
    private void AddTraderToLocales(TraderBase baseJson)
    {
        var newTraderId = baseJson.Id;
        customLocales.AddLocale($"{newTraderId} FullName", "Trader.FullName");
        customLocales.AddLocale($"{newTraderId} FirstName", "Trader.FirstName");
        customLocales.AddLocale($"{newTraderId} Nickname", "Trader.Nickname");
        customLocales.AddLocale($"{newTraderId} Location", "Trader.Location");
        customLocales.AddLocale($"{newTraderId} Description", "Trader.Description");
    }
    private void RegisterQuestImages()
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var questImagesPath = Path.Combine(pathToMod, "res", "quests");
        if (!Directory.Exists(questImagesPath))
        {
            return;
        }

        var files = Directory.GetFiles(questImagesPath, "*.png", SearchOption.TopDirectoryOnly);
        foreach (var file in files)
        {
            var imageName = Path.GetFileNameWithoutExtension(file);
            imageRouter.AddRoute($"/files/quest/icon/{imageName}", file);
            questImages.Add(imageName);
        }
    }
}
