using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Web;
using ModConfigEditor;

namespace DefinitiveWeaponVariants;

public record ModMetadata : IModMetadata, IModBlazorMetadata
{
    // Opt into SPT's existing Blazor host. The actual page is compiled from
    // BlazorConfig/ConfigPage.razor; no separate web server or executable exists.
    public string? WWWRootUrl { get; init; }
    public string? HomePage { get; init; } = ModEditorSettings.Route;
    public string? HomePageDescription { get; init; } = ModEditorSettings.Description;
    public string ModGuid { get; init; } = "com.szonszczyk.definitiveweaponvariants";
    public string Name { get; init; } = "DefinitiveWeaponVariants";
    public string Author { get; init; } = "Szonszczyk";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("5.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.2");
    public List<string>? Incompatibilities { get; init; } = [];
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = [];
    public string? Url { get; init; } = "https://github.com/Szonszczyk/DefinitiveWeaponVariants-csharp";
    public string? License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}
