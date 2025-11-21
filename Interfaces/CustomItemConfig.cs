using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace DefinitiveWeaponVariants.Interfaces
{
    public class CustomItemConfig
    {
        public bool AirdropBlacklisted { get; set; } = true;
        public bool FenceBlacklisted { get; set; } = true;
        public bool FleaBlacklisted { get; set; } = true;
        public List<string> AddToInventorySlots { get; set; } = [];
        public string MasteryName { get; set; } = "";
        public Dictionary<MongoId, Preset> Presets { get; set; } = [];
    }
}
