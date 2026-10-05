namespace DefinitiveWeaponVariants.Models;

public class CustomBarterConfig
{
    public string TraderId { get; set; } = "ee840a5ba014e9c5478e2137";
    public int LoyalLevel { get; set; } = 0;
    public bool UnlimitedCount { get; set; } = true;
    public double StackObjectsCount { get; set; } = 99;
    public Dictionary<string, int> BarterPrice { get; set; } = [];
    public double? RandomAssortWeight { get; set; }
}