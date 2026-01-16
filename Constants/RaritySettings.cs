namespace DefinitiveWeaponVariants.Constants
{
    public enum Rarity
    {
        Meme,
        Flawed,
        Baseline,
        Niche,
        Advanced,
        Superior,
        Ultimate,
        Unique
    }

    public static class RaritySettings
    {
        public static readonly IReadOnlyDictionary<Rarity, RarityData> Values =
            new Dictionary<Rarity, RarityData>
            {
                [Rarity.Unique] = new RarityData
                {
                    Color = "#ff1493",
                    BgColor = "tracerRed",
                    Description = "This Unique-quality variant is the best possible combination of 2 or more variant types in one weapon",
                    Flavour = "Be blessed by Tarkov gods!",
                    StarRating = "★★★★★",
                    PriceMultiplier = 2.5
                },
                [Rarity.Ultimate] = new RarityData
                {
                    Color = "#ca1f2b",
                    BgColor = "red",
                    Description = "This Ultimate-quality variant is capable of outperforming even the best modified weapons",
                    Flavour = "You earned it, now use it to your advantage. Kick some scav ass or something!",
                    StarRating = "★★★★",
                    PriceMultiplier = 1.8
                },
                [Rarity.Superior] = new RarityData
                {
                    Color = "#ca741f",
                    BgColor = "orange",
                    Description = "This Superior-quality variant offers outstanding performance",
                    Flavour = "With some training and good attachments, this weapon can turn you into a machine of destruction!",
                    StarRating = "★★★",
                    PriceMultiplier = 1.4
                },
                [Rarity.Advanced] = new RarityData
                {
                    Color = "#f6f15d",
                    BgColor = "yellow",
                    Description = "This Advanced-quality variant offers better performance than standard weapons",
                    Flavour = "This is a very good weapon, but don't rely only on its power. Weapons don't play, users do!",
                    StarRating = "★★",
                    PriceMultiplier = 1.2
                },
                [Rarity.Niche] = new RarityData
                {
                    Color = "#75339c",
                    BgColor = "violet",
                    Description = "This Niche-quality variant is best used in very specific circumstances and situations",
                    Flavour = "Maybe you don't know when you'll need the help of a 'Blicky Rifle Grenade Launcher' right now, but you will when the time comes...",
                    StarRating = "★",
                    PriceMultiplier = 1.2
                },
                [Rarity.Baseline] = new RarityData
                {
                    Color = "#2694da",
                    BgColor = "blue",
                    Description = "This Baseline-quality variant performs on par with the original weapon",
                    Flavour = "Shiny weapon, but not that original. The stats are changed, but not by much. It's mid, I would say",
                    StarRating = "✧✧✧",
                    PriceMultiplier = 1.0
                },
                [Rarity.Flawed] = new RarityData
                {
                    Color = "#157230",
                    BgColor = "green",
                    Description = "This Flawed-quality variant delivers worse performance than the original weapon",
                    Flavour = "Despite being flawed, this weapon is still desired and used mostly by scavs, but only because of the low price and frequent sales",
                    StarRating = "✧✧",
                    PriceMultiplier = 0.7
                },
                [Rarity.Meme] = new RarityData
                {
                    Color = "#808080",
                    BgColor = "tracerGreen",
                    Description = "This Meme-quality variant trades everything to be good at one thing only",
                    Flavour = "A clown weapon for clown users, but remember to have fun before you die trying to kill someone with this god-forgotten thing",
                    StarRating = "✧",
                    PriceMultiplier = 0.7
                }
            };

        /// <summary>
        /// Returns RarityData by rarity name (case-insensitive). 
        /// Defaults to Baseline if not found.
        /// </summary>
        public static RarityData GetByName(string rarityName)
        {
            if (string.IsNullOrWhiteSpace(rarityName))
                return Values[Rarity.Baseline];

            if (Enum.TryParse<Rarity>(rarityName, true, out var rarity))
            {
                if (Values.TryGetValue(rarity, out var data))
                    return data;
            }

            // fallback
            return Values[Rarity.Baseline];
        }

        public static List<string> RarityList()
        {
            return ["Meme", "Flawed", "Baseline", "Niche", "Advanced", "Superior", "Ultimate", "Unique"];
        }
    }

    public class RarityData
    {
        public string Color { get; set; } = "";
        public string BgColor { get; set; } = "";
        public string Description { get; set; } = "";
        public string Flavour { get; set; } = "";
        public string StarRating { get; set; } = "";
        public double PriceMultiplier { get; set; }
    }
}
