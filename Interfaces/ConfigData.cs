namespace DefinitiveWeaponVariants.Interfaces
{
    public class ConfigData
    {
        // EASY
        public Dictionary<string, bool> Airdrop { get; set; } = [];
        public Dictionary<string, bool> Fence { get; set; } = [];
        public Dictionary<string, bool> Flea { get; set; } = [];
        public Dictionary<string, bool> Marked { get; set; } = [];
        public float MarkedRoomsProbability { get; set; } = 0;
        public Dictionary<string, bool> Generate { get; set; } = [];
        public CustomBarterConfig DWVCaliberBarter { get; set; } = new CustomBarterConfig();

        // ADVANCED
        public List<string> NotGenerateVariantTypes { get; set; } = [];
        public List<string> NotGenerateWeapons { get; set; } = [];
    }
}