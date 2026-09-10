using SPTarkov.Server.Core.Models.Spt.Mod;

namespace DefinitiveWeaponVariants;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.szonszczyk.definitiveweaponvariants";
    public string Name { get; init; } = "DefinitiveWeaponVariants";
    public string Author { get; init; } = "Szonszczyk";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("4.3.2");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.2");
    public List<string>? Incompatibilities { get; init; } = [];
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = [];
    public string? Url { get; init; } = "https://github.com/Szonszczyk/DefinitiveWeaponVariants-csharp";
    public string? License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}