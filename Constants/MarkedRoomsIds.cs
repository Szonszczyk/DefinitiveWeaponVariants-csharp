namespace DefinitiveWeaponVariants.Constants
{
    internal static class MarkedRoomsIds
    {
        public static readonly List<string> bigmap =
        [
            "Loot 135 (8)",
            "Loot 135 (9)",
            "Loot 135 (10)",
            "Loot 135 (11)",
            "Loot 135 (12)",
            "Loot 135 (13)",
            "Loot 135 (14)",
            "Loot 135 (15)"
        ];

        public static readonly List<string> rezervbase =
        [
            "cult_Loot 135 (11)",
            "cult_Loot 135 (12)",
            "cult_Loot 135 (13)",
            "cult_Loot 135 (14)",
            "cult_Loot 135 (15)",
            "cult_Loot 135 (16)",
            "cult_Loot 135 (17)",
            "cult_Loot 135 (18)",
            "Loot 135 (9)",
            "Loot 135 (10)",
            "Loot 135 (11)",
            "Loot 135 (12)",
            "Loot 135 (13)",
            "Loot 135 (14)",
            "Loot 135 (15)",
            "Loot 135 (16)",
            "Loot 135 (17)",
            "Loot 135 (18)",
            "loot_jeverly (10)",
            "loot_jeverly (14)"
        ];

        public static readonly List<string> tarkovstreets =
        [
            "Loot 135_Leo_Rare (8)",
            "Loot 135_Leo_Rare (9)",
            "Loot 135_Leo_Rare (10)",
            "Loot 135_Leo_Rare (13)",
            "Loot 135_Leo_Rare (15)",
            "Loot 135_Leo_Rare (19)",
            "Loot 135_Leo_Rare (20)",
            "Loot 135_Leo_Rare (21)",
            "Loot 135_Leo_Rare (29)",
            "Loot 135_Leo_Rare (30)",
            "Loot 135_Leo_Rare (31)",
            "Loot 135_Leo_Rare (10)",
            "Loot 135_Leo_Rare (10)",
            "Loot 135_Leo_Rare (31)",
            "Loot 135_Leo_Rare (41)",
            "Loot 135_Leo_Rare (43)",
            "Loot 135_Leo_Rare (44)",
            "Loot 135_Leo_Rare (45)",
            "Loot 135_Leo_Rare (46)"
        ];

        private static readonly Dictionary<string, List<string>> _mapLookup = new(StringComparer.OrdinalIgnoreCase)
        {
            { "bigmap", bigmap },
            { "rezervbase", rezervbase },
            { "tarkovstreets", tarkovstreets }
        };

        /// <summary>
        /// Returns the list of marked room IDs for the given map name.
        /// </summary>
        public static List<string>? GetListByName(string mapName)
        {
            return _mapLookup.TryGetValue(mapName, out var list) ? list : null;
        }

        /// <summary>
        /// Checks if the given input string contains any of the marked room identifiers
        /// for the specified map name.
        /// </summary>
        public static bool IsMarkedRoom(string mapName, string input)
        {
            var list = GetListByName(mapName);
            if (list == null || string.IsNullOrEmpty(input))
                return false;

            return list.Any(id => input.Contains(id, StringComparison.OrdinalIgnoreCase));
        }
    }
}