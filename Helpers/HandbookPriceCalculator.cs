namespace DefinitiveWeaponVariants.Helpers;

public static class HandbookPriceCalculator
{
    public const string RoublesTemplateId = "5449016a4bdc2d6f028b456f";

    // Mixed barters use the recognized rouble/core subtotal, as configured by the user.
    public static double Calculate(IReadOnlyDictionary<string, int> barterPrice, double fallback,
        Func<string, double?> corePrice)
    {
        double total = 0;
        bool recognized = false;
        foreach (var (item, count) in barterPrice)
        {
            var price = item is "MONEY_ROUBLES" or RoublesTemplateId ? 1.0 : corePrice(item);
            if (price is null) continue;
            recognized = true;
            total += price.Value * count;
        }
        return recognized ? total : fallback;
    }
}
