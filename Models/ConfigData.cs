namespace DefinitiveWeaponVariants.Models;

// Serialized property names and defaults follow config/configSchema.json.
public class ConfigData
{
    // Weapon Generation
    public Dictionary<string, bool> Generate { get; set; } = new()
    {
        ["Unique"] = true,
        ["Ultimate"] = true,
        ["Superior"] = true,
        ["Advanced"] = true,
        ["Niche"] = true,
        ["Baseline"] = true,
        ["Flawed"] = true,
        ["Meme"] = true,
    };
    public List<string> NotGenerateVariantTypes { get; set; } = [];
    public List<string> NotGenerateWeapons { get; set; } = [];

    // Quality Weights
    public Dictionary<string, int> QualityWeights { get; set; } = new()
    {
        ["Unique"] = 1,
        ["Ultimate"] = 2,
        ["Superior"] = 4,
        ["Advanced"] = 8,
        ["Niche"] = 6,
        ["Baseline"] = 12,
        ["Flawed"] = 16,
        ["Meme"] = 20,
    };

    // Loot Sources - Airdrops, Fence and Flea Market
    public Dictionary<string, bool> Airdrop { get; set; } = new()
    {
        ["Unique"] = false,
        ["Ultimate"] = true,
        ["Superior"] = true,
        ["Advanced"] = true,
        ["Niche"] = true,
        ["Baseline"] = true,
        ["Flawed"] = true,
        ["Meme"] = false,
    };
    public Dictionary<string, bool> Fence { get; set; } = new()
    {
        ["Unique"] = false,
        ["Ultimate"] = false,
        ["Superior"] = false,
        ["Advanced"] = false,
        ["Niche"] = false,
        ["Baseline"] = false,
        ["Flawed"] = false,
        ["Meme"] = false,
    };
    public Dictionary<string, bool> Flea { get; set; } = new()
    {
        ["Unique"] = false,
        ["Ultimate"] = false,
        ["Superior"] = false,
        ["Advanced"] = false,
        ["Niche"] = false,
        ["Baseline"] = false,
        ["Flawed"] = false,
        ["Meme"] = false,
    };

    // Loot Sources - Marked Rooms and Containers
    public Dictionary<string, bool> Marked { get; set; } = new()
    {
        ["Unique"] = true,
        ["Ultimate"] = true,
        ["Superior"] = true,
        ["Advanced"] = true,
        ["Niche"] = true,
        ["Baseline"] = false,
        ["Flawed"] = false,
        ["Meme"] = false,
    };
    public float MarkedRoomsProbability { get; set; } = 0.3f;
    public Dictionary<string, bool> StaticLoot { get; set; } = new()
    {
        ["Unique"] = false,
        ["Ultimate"] = true,
        ["Superior"] = true,
        ["Advanced"] = true,
        ["Niche"] = false,
        ["Baseline"] = true,
        ["Flawed"] = true,
        ["Meme"] = true,
    };
    public float StaticLootProbability { get; set; } = 0.3f;

    // DWV Trader - Merchant
    public int TraderDynamicOfferCount { get; set; } = 15;
    public RefreshInterval TraderRefreshSeconds { get; set; } = new()
    {
        Min = 900,
        Max = 1800,
    };
    public bool SpecialAmmoBuyableEnabled { get; set; } = true;
    public CustomBarterConfig DWVCaliberBarter { get; set; } = new()
    {
        LoyalLevel = 1,
        BarterPrice = new() { ["MONEY_ROUBLES"] = 2500 },
    };

    // Variant Cores - General
    public bool ModCoreSlotEnabled { get; set; } = true;
    public Dictionary<string, int> VariantCoresPrice { get; set; } = new()
    {
        ["Unique"] = 15000,
        ["Ultimate"] = 10000,
        ["Superior"] = 8000,
        ["Advanced"] = 5000,
        ["Niche"] = 6000,
        ["Baseline"] = 4000,
        ["Flawed"] = 3000,
        ["Meme"] = 2000,
    };
    public bool VariantCoreSlotRequired { get; set; } = false;

    // Variant Cores - Normal
    public int VCNormalErgonomics { get; set; } = 5;
    public bool VCNormalBuyable { get; set; } = false;
    public CustomBarterConfig VCNormalBarter { get; set; } = new()
    {
        LoyalLevel = 1,
        UnlimitedCount = false,
        StackObjectsCount = 5,
    };
    public bool VCNormalUpgradable { get; set; } = true;
    public string VCNormalUpgradeUpToQuality { get; set; } = "Baseline";
    public int VCNormalUpgradeRatio { get; set; } = 3;
    public CustomBarterConfig VCNormalUpgradeBarter { get; set; } = new()
    {
        LoyalLevel = 1,
        UnlimitedCount = false,
        StackObjectsCount = 5,
    };
    public Dictionary<string, float> VCNormalFoundOnEnemies { get; set; } = new()
    {
        ["assault"] = 0.33f,
    };
    public int VCNormalPriceMultiplier { get; set; } = 1;

    // Variant Cores - Locked
    public int VCLockedErgonomics { get; set; } = 7;
    public int VCLockedAccuracy { get; set; } = 3;
    public int VCLockedUpgradeRatio { get; set; } = 3;
    public CustomBarterConfig VCLockedUpgradeBarter { get; set; } = new()
    {
        LoyalLevel = 2,
        UnlimitedCount = false,
        StackObjectsCount = 5,
    };
    public int VCLockedPriceMultiplier { get; set; } = 6;

    // Unknown Packages
    public bool UnknownPackageEnabled { get; set; } = true;
    public InventoryItemProperties UnknownPackageProperties { get; set; } = new()
    {
        Width = 2,
        Height = 1,
        Weight = 1.2,
    };
    public CustomBarterConfig UnknownPackageBarter { get; set; } = new()
    {
        LoyalLevel = 1,
        UnlimitedCount = false,
        StackObjectsCount = 20,
        BarterPrice = new() { ["MONEY_ROUBLES"] = 250000 },
    };
    public int UnknownPackageCoresReceived { get; set; } = 5;
    public float UnknownPackageProbability { get; set; } = 0.33f;

    // Variant Cores - Universal
    public int VCUniversalToUniqueRatio { get; set; } = 1;

    // Variant Weapon Blind Boxes
    public bool BlindBoxesEnabled { get; set; } = true;
    public InventoryItemProperties BlindBoxesProperties { get; set; } = new()
    {
        Width = 2,
        Height = 1,
        Weight = 1.2,
    };
    public int BlindBoxesCoresPrice { get; set; } = 3;
    public CustomBarterConfig BlindBoxesBarter { get; set; } = new()
    {
        LoyalLevel = 1,
        UnlimitedCount = false,
        StackObjectsCount = 20,
    };
    public int BlindBoxesPriceMultiplier { get; set; } = 6;

    // Expeditionary Case
    public ExpeditionaryCaseGridProperties ExpCaseGridProperties { get; set; } = new()
    {
        CellsH = 1,
        CellsV = 2,
    };
    public InventoryItemProperties ExpCaseProperties { get; set; } = new()
    {
        Width = 1,
        Height = 1,
        Weight = 1.2,
    };
    public CustomBarterConfig ExpCaseBarter { get; set; } = new()
    {
        LoyalLevel = 1,
        UnlimitedCount = false,
        StackObjectsCount = 5,
        BarterPrice = new() { ["Baseline Quality Variant Core"] = 12 },
    };
    public int ExpCaseHandbookPriceRoubles { get; set; } = 48000;

    // Carry Case
    public Dictionary<string, int> CarryCaseGridTypesWidth { get; set; } = new()
    {
        ["normal"] = 4,
        ["universal"] = 2,
        ["other"] = 5,
    };
    public int CarryCaseGridHeight { get; set; } = 10;
    public InventoryDimensions CarryCaseProperties { get; set; } = new()
    {
        Width = 5,
        Height = 2,
    };
    public CustomBarterConfig CarryCaseBarter { get; set; } = new()
    {
        LoyalLevel = 1,
        UnlimitedCount = false,
        StackObjectsCount = 10,
        BarterPrice = new() { ["Baseline Quality Variant Core"] = 30 },
    };
    public int CarryCaseHandbookPriceRoubles { get; set; } = 120000;

    // Integrations - APBS
    public bool EnableAPBSBlacklistGeneration { get; set; } = false;
    public string APBSFolderName { get; set; } = "acidphantasm-progressivebotsystem";
    public Dictionary<string, int> APBSTierConfig { get; set; } = new()
    {
        ["Unique"] = 0,
        ["Ultimate"] = 7,
        ["Superior"] = 6,
        ["Advanced"] = 5,
        ["Niche"] = 4,
        ["Baseline"] = 3,
        ["Flawed"] = 2,
        ["Meme"] = 1,
    };
    public List<string> APBSBlacklistedVariantTypes { get; set; } = ["Rifle Grenade Launcher"];
    public bool EnableAPBSWeaponRotation { get; set; } = false;
    public int APBSWeaponsAvailable { get; set; } = 75;
    public int APBSWeaponRotationIntervalMinutes { get; set; } = 60;

    // Other
    public bool Debug { get; set; } = false;

}

public class InventoryDimensions
{
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
}

public class InventoryItemProperties : InventoryDimensions
{
    public double Weight { get; set; } = 1.2;
}

public class ExpeditionaryCaseGridProperties
{
    public int CellsH { get; set; } = 1;
    public int CellsV { get; set; } = 2;
}
