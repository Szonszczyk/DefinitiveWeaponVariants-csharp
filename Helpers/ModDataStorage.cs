using DefinitiveWeaponVariants.Constants;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.Helpers;

[Injectable(InjectionType.Singleton)]
public class ModDataStorage(ICloner cloner)
{
    public Globals GlobalsData { get; private set; } = null!;
    public Dictionary<MongoId, TemplateItem> Items { get; private set; } = null!;
    public Dictionary<MongoId, Trader> Traders { get; private set; } = null!;
    public Dictionary<MongoId, Quest> Quests { get; private set; } = null!;
    public SPTarkov.Server.Core.Models.Spt.Hideout.Hideout HideoutData { get; private set; } = null!;
    public HideoutConfig HideoutConfigData { get; private set; } = null!;
    public AirdropConfig ConfigServerAirdropConfig { get; private set; } = null!;
    public TraderConfig ConfigServerTraderConfig { get; private set; } = null!;
    public RagfairConfig ConfigServerRagfairConfig { get; private set; } = null!;
    public HandbookBase Handbook { get; private set; } = null!;
    public InventoryConfig InventoryConfigData { get; private set; } = null!;
    public Dictionary<string, string> LocaleEn { get; private set; } = null!;

    public void Initialize(DatabaseService databaseService, ConfigServer configServer, LocaleService localeService)
    {
        GlobalsData = databaseService.GetGlobals();
        Items = databaseService.GetItems();
        Traders = databaseService.GetTraders();
        Quests = databaseService.GetQuests();
        HideoutData = databaseService.GetHideout();
        HideoutConfigData = configServer.GetConfig<HideoutConfig>();
        ConfigServerAirdropConfig = configServer.GetConfig<AirdropConfig>();
        ConfigServerTraderConfig = configServer.GetConfig<TraderConfig>();
        ConfigServerRagfairConfig = configServer.GetConfig<RagfairConfig>();
        Handbook = databaseService.GetHandbook();
        InventoryConfigData = configServer.GetConfig<InventoryConfig>();
        LocaleEn = localeService.GetLocaleDb("en");
    }

    public static bool IsPluginLoaded()
    {
        const string pluginName = "rairai.colorconverterapi.dll";
        const string pluginsPath = "../BepInEx/plugins";

        try
        {
            if (!Directory.Exists(pluginsPath))
                return false;

            var pluginList = Directory.GetFiles(pluginsPath)
                .Select(System.IO.Path.GetFileName)
                .Select(f => f?.ToLowerInvariant());
            return pluginList.Contains(pluginName);
        }
        catch
        {
            return false;
        }
    }

    // Database of items in preset 
    public Dictionary<string, List<List<Item>>> VariantPresets { get; private set; }  = [];
    // Database of all variant Ids
    public HashSet<string> AllVariantIds { get; private set; } = [];
    // Database of variant Ids keyd by quality
    public Dictionary<string, HashSet<string>> VariantIdsByQuality { get; private set; } = [];
    // Dictionary of variant id <=> variant type name
    public Dictionary<string, string> VariantTypes { get; private set; } = [];

    public void AddVariantToStorage(string id, string quality, string variantType, Dictionary<MongoId, Preset> presets)
    {
        if (presets.Count > 0)
        {
            var presetItems = presets.First().Value.Items;
            var items = cloner.Clone(presetItems);
            if (items?.Count == 0 || items is null) return;

            if (VariantPresets.TryGetValue(quality, out var qualityWeapons))
                qualityWeapons.Add(items);
            else
                VariantPresets.Add(quality, [items]);
        }
        
        AllVariantIds.Add(id);
        VariantTypes.Add(id, variantType);
        if (VariantIdsByQuality.TryGetValue(quality, out var variantList))
            variantList.Add(id);
        else
            VariantIdsByQuality.Add(quality, [id]);
    }

    public Dictionary<string, List<string>> CoresByType { get; private set; } = new()
    {
        ["normal"] = [],
        ["universal"] = [],
        ["other"] = []
    }; 
    public Dictionary<string, List<MongoId>> CoresByQuality { get; private set; } = [];
    public HashSet<MongoId> CoreIds { get; private set; } = [];

    public void AddCoreToStorage(string id, string quality, string type)
    {
        switch(type)
        {
            case "normal":
                if (CoresByQuality.TryGetValue(quality, out var coreList))
                    coreList.Add(id);
                else
                    CoresByQuality.Add(quality, [id]);
                break;
            case "universal":
                foreach (var (_, coreListInQuality) in CoresByQuality)
                {
                    coreListInQuality.Add(id);
                }
                break;
            case "other":
                break;
        }
        CoresByType[type].Add(id);
        CoreIds.Add(id);
    }

    public Dictionary<string, List<string>> ItemsByQuality { get; private set; } = [];

    public void AddItemToQuality(string id, string rarity)
    {
        ItemsByQuality.TryGetValue(rarity, out var mongoIds);
        if (mongoIds is null)
        {
            ItemsByQuality.Add(rarity, [id]);
        } else
        {
            mongoIds.Add(id);
        }
    }

    public void FixBackgroundColors()
    {
        foreach(var (qualityName, itemIds) in ItemsByQuality)
        {
            var quality = RaritySettings.GetByName(qualityName);
            var color = IsPluginLoaded() ? quality.Color : quality.BgColor;
            foreach (var itemId in itemIds)
            {
                Items.TryGetValue(itemId, out var item);
                if (item != null)
                {
                    if (item?.Properties?.BackgroundColor is null || item.Properties.BackgroundColor == color) continue;
                    item.Properties.BackgroundColor = color;
                }
            }
        }
    }
}
