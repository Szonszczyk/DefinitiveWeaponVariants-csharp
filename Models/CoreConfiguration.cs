using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace DefinitiveWeaponVariants.Models;

public class CoreConfiguration
{
    public CoreTheme Theme { get; set; } = new();
    public Dictionary<string, CoreItemConfiguration> Items { get; set; } = [];
}

public class CoreTheme
{
    public string Color1 { get; set; } = string.Empty;
    public string Color2 { get; set; } = string.Empty;
}

public class CoreItemConfiguration
{
    public TemplateItemProperties Properties { get; set; } = new();
}
