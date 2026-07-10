using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace DefinitiveWeaponVariants.Interfaces;

public class ConfigData
{
    // EASY
    public Dictionary<string, bool> Generate { get; set; } = []; // 1.1.
    public Dictionary<string, bool> Airdrop { get; set; } = []; // 2.1.
    public Dictionary<string, bool> Fence { get; set; } = []; // 2.2.
    public Dictionary<string, bool> Flea { get; set; } = []; // 2.3.
    public Dictionary<string, bool> Marked { get; set; } = []; // 2.4.
    public Dictionary<string, bool> StaticLoot { get; set; } = []; // 2.5.
    public bool VariantCoresEnabled { get; set; } = true;  // 3.1.
    public bool BlindBoxesEnabled { get; set; } = true;  // 3.2.
    public bool SpecialAmmoBuyableEnabled { get; set; } = true;  // 4.1.
    public bool EnableAPBSBlacklistGeneration { get; set; } = false;  // 5.1.
    public string APBSFolderName { get; set; } = "acidphantasm-progressivebotsystem";  // 5.1.
    public bool AmonyaTraderMode { get; set; } = false;  // 5.2.

    // ADVANCED
    public List<string> NotGenerateVariantTypes { get; set; } = []; // 10.1.
    public List<string> NotGenerateWeapons { get; set; } = []; // 10.2.
    public Dictionary<string, int> QualityWeights { get; set; } = []; // 11.1.
    public float MarkedRoomsProbability { get; set; } = 0.3f; // 12.1. [2.4.]
    public float StaticLootProbability { get; set; } = 0.3f; // 12.1. [2.5.]
    public VariantCoresTypesConfigData VariantCores { get; set; } = new();
    public CustomBarterConfig DWVCaliberBarter { get; set; } = new CustomBarterConfig(); // 14.1.
    public Dictionary<string, int> APBSTierConfig { get; set; } = [];  // 15.1.
    public List<string> APBSBlacklistedVariantTypes { get; set; } = [];  // 15.1.
}

public class VariantCoresTypesConfigData
{
    public VariantCoresGeneralConfigData General { get; set; } = new();
    public VariantCoresNormalConfigData Normal { get; set; } = new();
    public VariantCoresLockedConfigData Locked { get; set; } = new();
    public VariantCoresUniversalConfigData Universal { get; set; } = new();
    public VariantCoresBlindBoxesConfigData BlindBoxes { get; set; } = new();
    public VariantCoresUnknownPackageConfigData UnknownPackage { get; set; } = new();
    public VariantCoresExpeditionaryCaseConfigData ExpeditionaryCase { get; set; } = new();
    public VariantCoresCarryCaseConfigData CarryCase { get; set; } = new();
}

public class VariantCoresGeneralConfigData
{
    public Dictionary<string, int> Price { get; set; } = new()
    {
        ["Unique"] = 15000,
        ["Ultimate"] = 10000,
        ["Superior"] = 8000,
        ["Advanced"] = 5000,
        ["Niche"] = 6000,
        ["Baseline"] = 4000,
        ["Flawed"] = 3000,
        ["Meme"] = 2000
    };
    public bool Required { get; set; } = false;
    public int StackMaxSize { get; set; } = 10;
}

public class VariantCoresNormalConfigData
{
    public TemplateItemProperties Properties { get; set; } = new() { Ergonomics = 5 };
    public bool Buyable { get; set; } = false;
    public CustomBarterConfig Barter { get; set; } = new();
    public bool Upgradable { get; set; } = false;
    public UpgradableOptionsConfig UpgradableOptions { get; set; } = new();
    public Dictionary<string, float> FoundOnEnemies { get; set; } = new() { ["assault"] = 0.33f };
    public int PriceMultiplier { get; set; } = 1;
}
public class VariantCoresLockedConfigData
{
    public TemplateItemProperties Properties { get; set; } = new() {
        Ergonomics = 7,
        Accuracy = 3
    };
    public bool Upgradable { get; set; } = true;
    public UpgradableOptionsConfig UpgradableOptions { get; set; } = new();
    public int PriceMultiplier { get; set; } = 6;
}
public class VariantCoresUniversalConfigData
{
    public TemplateItemProperties Properties { get; set; } = new() {
        Ergonomics = 7,
        Recoil = 0
    };
}
public class VariantCoresBlindBoxesConfigData
{
    public TemplateItemProperties Properties { get; set; } = new()
    {
        Width = 3,
        Height = 2
    };
    public int CoresPrice { get; set; } = 3;
    public CustomBarterConfig Barter { get; set; } = new();
    public int PriceMultiplier { get; set; } = 25;
}
public class VariantCoresUnknownPackageConfigData
{
    public TemplateItemProperties Properties { get; set; } = new()
    {
        Width = 2,
        Height = 1,
        Weight = 1.2
    };
    public int PriceInRoubles { get; set; } = 250000;
    public CustomBarterConfig Barter { get; set; } = new();
    public int CoresReceived { get; set; } = 5;
    public float Probability { get; set; } = 0.33f;
}
public class VariantCoresExpeditionaryCaseConfigData
{
    public ExpeditionaryCaseGridProperties GridProperties { get; set; } = new();
    public TemplateItemProperties Properties { get; set; } = new()
    {
        Width = 1,
        Height = 1,
        Weight = 1.2
    };
    public string PriceCoreQuality { get; set; } = "Baseline";
    public int PriceCoreAmount { get; set; } = 12;
    public CustomBarterConfig Barter { get; set; } = new();
}
public class ExpeditionaryCaseGridProperties
{
    public int CellsH { get; set; } = 1;
    public int CellsV { get; set; } = 2;
}
public class VariantCoresCarryCaseConfigData
{
    public Dictionary<string, int> GridTypesWidth { get; set; } = new() {
        ["normal"] = 4,
        ["universal"] = 1,
        ["other"] = 5
    };
    public int GridHeight { get; set; } = 10;
    public TemplateItemProperties Properties { get; set; } = new()
    {
        Width = 5,
        Height = 2
    };
    public string PriceCoreQuality { get; set; } = "Baseline";
    public int PriceCoreAmount { get; set; } = 30;
    public CustomBarterConfig Barter { get; set; } = new();
}
public class UpgradableOptionsConfig
{
    public string UpToQuality { get; set; } = "Baseline";
    public int Ratio { get; set; } = 3;
    public CustomBarterConfig Barter { get; set; } = new();
}
