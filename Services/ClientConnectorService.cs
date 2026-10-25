using System.Text.Json.Serialization;
using DefinitiveWeaponVariants.Helpers;
using SPTarkov.DI.Annotations;

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
            VariantColors = new Dictionary<string, string>(
                modDataStorage.ColorByVariant,
                StringComparer.Ordinal)
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
            ItemColors = new Dictionary<string, List<string>>(
                modDataStorage.ColorsByItems,
                StringComparer.Ordinal)
        };
    }
}