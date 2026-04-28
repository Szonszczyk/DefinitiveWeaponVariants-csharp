using SPTarkov.Server.Core.Models.Common;

namespace DefinitiveWeaponVariants.Interfaces
{
    public class VariantConfiguration
    {
        // [variant/item] Description
        public string? Description { get; set; } = string.Empty;
        // [variant] Explanation of what is the variant doing
        public string? Explanation { get; set; } = string.Empty;
        // [variant/item] ShortName
        public string? ShortName { get; set; } = string.Empty;
        // [item] Item MongoId to clone
        public MongoId? ItemTplToClone { get; set; }

        // [variant/item] Dictionary for mixed-type values, needs to be set to type of default one or string => integer/float
        public Dictionary<string, object>? Properties { get; set; }
        // [variant/item] Changes made to slots/chambers/cartidges
        
        public ChangeSet? Changes { get; set; }
        // [variant] IndividualChanges of given Weapon in Variant
        public Dictionary<string, IndividualChangeSet>? IndividualChanges { get; set; }
        // [variant/item] To add to trader for barter
        public CustomBarterConfig? Barter {  get; set; }
        // [variant] Add to quest as X weapon instead of base weapon
        public string? WeaponIdToUseAs { get; set; }
        // [variant] Array of ShortName (db/03_Shortnames) of weapons that will have this variant
        public List<string> Weapons { get; set; } = [];
        // [item] Handbook Price in Roubles
        public double? HandbookPriceRoubles { get; set; }
        // [item] What this item is for which variant
        public string? VariantType { get; set; } = string.Empty;
        // [variant/item] Should be one from Constants/RaritySettings.cs
        public string? Rarity { get; set; } = string.Empty;
    }

    public class ChangeSet
    {
        // [variant/item] Change parent MongoId to other than this of base weapon
        public string? Parent { get; set; }
        public List<string>? AddtoInventorySlots { get; set; }
        // [variant] Minimum value of integer/float property
        public Dictionary<string, double>? Minimum { get; set; }
        // [variant/item] Change slots
        public Dictionary<string, FilterSlotExtendedConfiguration>? Slots { get; set; }
        // [variant/item] Change chambers
        public FilterSlotExtendedConfiguration? Chambers { get; set; }
        // [item] Change type and count of cartidges in magazine
        public FilterSlotExtendedConfiguration? Cartridges { get; set; }
    }

    public class IndividualChangeSet
    {
        public Dictionary<string, object>? Properties { get; set; }

        public Dictionary<string, FilterSlotExtendedConfiguration>? Slots { get; set; }

        public FilterSlotExtendedConfiguration? Chambers { get; set; }
    }

    public class FilterSlotConfiguration
    {
        // [item] Count of cartidges in magazine
        public double? Count { get; set; }
        // [variant/item] Replace filter with this array
        public List<string>? Filter { get; set; }
        // [variant/item] Get filter from item id of the same slot/Chambers/Cartridges
        public List<string>? FromWeapons { get; set; }
        // [variant/item] Add to existing filter
        public List<string>? Add { get; set; }
        // [variant/item] Is this slot required (slot only)
        public bool? Required { get; set; }
    }

    public class FilterSlotExtendedConfiguration : FilterSlotConfiguration
    {
        public BasedOnConfiguration? BasedOn { get; set; }
    }

    public class BasedOnConfiguration
    {
        public string Property { get; set; } = string.Empty;

        public Dictionary<string, FilterSlotConfiguration> Cases { get; set; } = new();
    }
}
