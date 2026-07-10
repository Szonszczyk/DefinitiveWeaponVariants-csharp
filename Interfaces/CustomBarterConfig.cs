namespace DefinitiveWeaponVariants.Interfaces;

public class CustomBarterConfig
{
    public string TraderId { get; set; } = "PEACEKEEPER";
    public int LoyalLevel { get; set; } = 0;
    public bool UnlimitedCount { get; set; } = true;
    public double StackObjectsCount { get; set; } = 99;
    public Dictionary<string, int> BarterPrice { get; set; } = [];

}