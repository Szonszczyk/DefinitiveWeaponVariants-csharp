namespace DefinitiveWeaponVariants.Interfaces
{
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
        public VariantCoresConfigData VariantCores { get; set; } = new VariantCoresConfigData(); // 13.1.
        public BlindBoxesConfigData BlindBoxes { get; set; } = new BlindBoxesConfigData(); // 13.1.
        public CustomBarterConfig DWVCaliberBarter { get; set; } = new CustomBarterConfig(); // 14.1.
        public Dictionary<string, int> APBSTierConfig { get; set; } = [];  // 15.1.
        public List<string> APBSBlacklistedVariantTypes { get; set; } = [];  // 15.1.
    }

    public class VariantCoresConfigData
    {
        public Dictionary<string, float> FoundOnEnemies { get; set; } = new() { ["assault"] = 0.33f };
        public bool Required { get; set; } = false;
        public int Ergonomics { get; set; } = 5;
        public bool Buyable { get; set; } = false;
        public CustomBarterConfig CoresBarter { get; set; } = new CustomBarterConfig();
        public Dictionary<string, int> Price { get; set; } = [];
        public int LockedCoresErgonomics { get; set; } = 7;
        public int LockedCoresPrice { get; set; } = 3;
        public CustomBarterConfig LockedCoresBarter { get; set; } = new CustomBarterConfig();
        public float UnknownPackageProbability { get; set; } = 0.33f;
        public int UnknownPackagePriceInRoubles { get; set; } = 250000;
    }

    public class BlindBoxesConfigData
    {
        public Dictionary<string, int> Price { get; set; } = [];
        public int CoresPrice { get; set; } = 3;
        public CustomBarterConfig BoxesBarter { get; set; } = new CustomBarterConfig();
        public int Width { get; set; } = 3;
        public int Height { get; set; } = 2;
    }
}