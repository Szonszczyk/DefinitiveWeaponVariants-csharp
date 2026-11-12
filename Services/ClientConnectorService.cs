using DefinitiveWeaponVariants.Helpers;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Text.Json.Serialization;

namespace DefinitiveWeaponVariants.Services;

[Injectable(InjectionType.Singleton)]
public class ClientConnectorService(ModDataStorage modDataStorage)
{
    public class RouterModelDWV
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; } = 1;

        [JsonPropertyName("serverModVersion")]
        public string ServerModVersion { get; } =  new ModMetadata().Version.ToString();
    }
    public class RouterModelVariantColors : RouterModelDWV
    {
        [JsonPropertyName("variantColors")]
        public Dictionary<string, string> VariantColors { get; init; } = [];
    }

    public RouterModelVariantColors PrepareRouterModelVariantColors()
    {
        return new RouterModelVariantColors
        {
            VariantColors = new Dictionary<string, string>(modDataStorage.WeaponVariantsInfo.GetWeaponColors(), StringComparer.Ordinal)
        };
    }

    public class RouterModelItemColors : RouterModelDWV
    {
        [JsonPropertyName("itemColors")]
        public Dictionary<string, List<string>> ItemColors { get; init; } = [];
    }

    public RouterModelItemColors PrepareRouterModelItemColors()
    {
        return new RouterModelItemColors
        {
            ItemColors = new Dictionary<string, List<string>>(modDataStorage.ColorsByItems, StringComparer.Ordinal)
        };
    }
    public class RouterModelAudioSync : RouterModelDWV
    {
        [JsonPropertyName("firerateAudioSync")]
        public Dictionary<string, FirerateAudioSyncModel> FirerateAudioSync { get; init; } = [];
    }
    public class FirerateAudioSyncModel
    {
        [JsonPropertyName("sourceTemplateId")]
        public string TemplateId { get; set; } = string.Empty;
        [JsonPropertyName("beforeFireRate")]
        public double FirerateBefore { get; set; } = 0;
        [JsonPropertyName("afterFireRate")]
        public double FirerateAfter { get; set; } = 0;
    }

    public RouterModelAudioSync PrepareRouterModelAudioSync()
    {
        return new RouterModelAudioSync
        {
            FirerateAudioSync = new Dictionary<string, FirerateAudioSyncModel>(modDataStorage.WeaponVariantsInfo
                .Where(x => x.Value.ChangedFirerate == true)
                .ToDictionary(x => x.Key, x => new FirerateAudioSyncModel()
                {
                    TemplateId = x.Value.TemplateId,
                    FirerateBefore = x.Value.FirerateBefore ?? 0,
                    FirerateAfter = x.Value.FirerateAfter ?? 0,
                }
            ), StringComparer.Ordinal)
        };
    }
}