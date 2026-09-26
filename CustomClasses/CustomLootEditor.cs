using SPTarkov.DI.Annotations;

namespace DefinitiveWeaponVariants.CustomClasses;

[Injectable(InjectionType.Singleton)]
public class CustomLootEditor
{
    public class ItemAddedToLoot
    {
        public string Id { get; set; } = string.Empty;
        public int ProbPerc { get; set; } = 0;
    }

    public Dictionary<string, List<ItemAddedToLoot>> itemsAddedToStaticLoot = [];
}
