using DefinitiveWeaponVariants.Constants;
using MudBlazor;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.Helpers;

[Injectable(InjectionType.Singleton)]
public class ModDataStorage(
    ICloner cloner,
    TemplateTable templateTable
)
{
    public DWVWeaponInfoCollection WeaponVariantsInfo { get; private set; } = [];

    public class DWVWeaponInfo
    {
        public string Quality { get; set; } = "Baseline";
        public string VariantType { get; set; } = "Modified";
        public string Color { get; set; } = "#2694da";
        public string TemplateId { get; set; } = string.Empty;
        public List<List<Item>> Presets { get; private set; } = [];
        public bool ChangedFirerate { get; set; } = false;
        public double? FirerateBefore { get; set; }
        public double? FirerateAfter { get; set; }
    }

    public class DWVWeaponInfoCollection : Dictionary<string, DWVWeaponInfo>
    {
        public List<List<Item>> GetPresetsByQuality(string quality) =>
            Values
                .Where(x => x.Quality == quality)
                .SelectMany(x => x.Presets)
                .ToList();

        public Dictionary<string, string> GetWeaponColors() =>
            this.ToDictionary(x => x.Key, x => x.Value.Color);

        public Dictionary<string, string> GetVariantTypes() =>
            this.ToDictionary(x => x.Key, x => x.Value.VariantType);

        public Dictionary<string, HashSet<string>> GetWeaponsByQuality() =>
            this.GroupBy(x => x.Value.Quality)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Key).ToHashSet()
                );
    }

    public void AddVariantToStorage(
        string id,
        string quality,
        string variantType,
        Dictionary<MongoId, Preset> presets,
        string templateId,
        double firerateBefore,
        double firerateAfter
    )
    {
        var newWeapon = new DWVWeaponInfo() {
            Quality = quality,
            VariantType = variantType,
            Color = RaritySettings.GetByName(quality).Color,
            TemplateId = templateId
        };
        
        if (presets.Count > 0)
        {
            var presetItems = presets.First().Value.Items;
            var items = cloner.Clone(presetItems);
            if (items?.Count > 0 && items != null)
            {
                newWeapon.Presets.Add(items);
            }
        }
        if (firerateBefore > 0 && firerateAfter > 0 && firerateBefore != firerateAfter)
        {
            newWeapon.ChangedFirerate = true;
            newWeapon.FirerateBefore = firerateBefore;
            newWeapon.FirerateAfter = firerateAfter;
        }
        WeaponVariantsInfo.Add(id, newWeapon);
    }

    // Dictionary of item id <=> List of colors 1-2
    public Dictionary<string, List<string>> ColorsByItems { get; private set; } = [];


    // Database Quality <> Core ID
    public Dictionary<string, string> CoreIDsByQuality { get; private set; } = [];
    public Dictionary<string, double> CoreHandbookPrices { get; } = [];

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
        switch (type)
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
        }
        else
        {
            mongoIds.Add(id);
        }
    }

    public void FixBackgroundColors()
    {
        foreach (var (qualityName, itemIds) in ItemsByQuality)
        {
            var quality = RaritySettings.GetByName(qualityName);
            var color = IsPluginLoaded() ? quality.Color : quality.BgColor;
            foreach (var itemId in itemIds)
            {
                templateTable.Items.TryGetValue(itemId, out var item);
                if (item != null)
                {
                    if (item?.Properties?.BackgroundColor is null || item.Properties.BackgroundColor == color) continue;
                    item.Properties.BackgroundColor = color;
                }
            }
        }
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
}
