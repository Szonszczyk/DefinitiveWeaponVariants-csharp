using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Models;
using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace DefinitiveWeaponVariants.Services;

/// <summary>
/// Captures complete trader source pools once, then publishes a single weighted rotation shared by every profile.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class WeightedAssortService(
    WeightReader weightReader,
    ICloner cloner,
    RandomUtil randomUtil,
    TimeUtil timeUtil,
    TraderHelper traderHelper,
    TraderConfig traderConfig,
    TradersTable tradersTable,
    CustomLogger logger,
    ConfigData config
)
{
    private const string Hideout = "hideout";
    private readonly Dictionary<MongoId, TraderPoolState> poolStates = [];

    public void InitializeConfiguredTraders()
    {
        if (!config.Trader.Enabled)
        {
            logger.Info("Random assort mod is disabled by configuration.");
            return;
        }

        foreach (var rule in config.Trader.Traders.Where(rule => rule.Enabled))
        {
            try
            {
                InitializeTrader(rule);
            }
            catch (Exception exception)
            {
                logger.Error($"Random assort rule for trader '{rule.TraderId}' was disabled: {exception.Message}");
                if (rule.StrictValidation) throw;
            }
        }
    }

    public void RefreshExpiredTraders()
    {
        var now = timeUtil.GetTimeStamp();

        foreach (var (traderId, state) in poolStates.ToArray())
        {
            try
            {
                if (!tradersTable.TryGetValue(traderId, out var trader))
                {
                    logger.Error($"Random assort trader '{traderId}' no longer exists. Its random assort functionality has been disabled.");
                    poolStates.Remove(traderId);
                    continue;
                }

                var expiredTimestamp = trader.Base.NextResupply;
                if (expiredTimestamp is null || expiredTimestamp > now || state.LastHandledExpiredTimestamp == expiredTimestamp) continue;

                InstallRotation(trader, state, isRefresh: true);
                state.LastHandledExpiredTimestamp = expiredTimestamp;
                trader.Base.RefreshTraderRagfairOffers = true;
            }
            catch (Exception exception)
            {
                logger.Error($"Could not refresh random assort for trader '{traderId}'. The previous complete rotation was kept: {exception.Message}");
            }
        }
    }

    private void InitializeTrader(TraderRule rule)
    {
        if (!MongoId.IsValidMongoId(rule.TraderId))
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("Trader ID is not a valid Mongo ID.");
        }

        if (string.IsNullOrWhiteSpace(rule.WeightProperty))
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("weightProperty cannot be empty.");
        }

        var traderId = (MongoId)rule.TraderId;
        if (poolStates.ContainsKey(traderId))
        {
            // Mod-developer validation: caught at the rule boundary and disables only this duplicate rule.
            throw new InvalidOperationException("The trader has more than one enabled random assort rule.");
        }

        if (traderId == Traders.FENCE)
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("Fence cannot be used with a random trader assort rule.");
        }

        if (!tradersTable.TryGetValue(traderId, out var trader) || trader.Assort is null)
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("Trader does not exist or has no assort.");
        }

        var state = CapturePool(rule, trader);
        EnsureRefreshInterval(rule, traderId);
        trader.Base.NextResupply = (int)traderHelper.GetNextUpdateTimestamp(traderId);
        InstallRotation(trader, state, isRefresh: false);
        poolStates.Add(traderId, state);

        logger.Debug(
            $"Sealed random assort pool for trader '{traderId}': "
            + $"{state.StaticOffers.Count} static root(s), {state.DynamicOffers.Count} dynamic root(s), "
            + $"{state.CurrentDynamicRootIds.Count} selected dynamic root(s)."
        );
    }

    private void EnsureRefreshInterval(TraderRule rule, MongoId traderId)
    {
        if (rule.RefreshSeconds.Min <= 0 || rule.RefreshSeconds.Max < rule.RefreshSeconds.Min)
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("refreshSeconds must have a positive minimum and a maximum at least as large.");
        }

        var matchingEntries = traderConfig.UpdateTime.Where(entry => entry.TraderId == traderId).ToList();
        if (matchingEntries.Count == 0)
        {
            traderConfig.UpdateTime.Add(new UpdateTime
            {
                TraderId = traderId,
                Seconds = new MinMax<int>(rule.RefreshSeconds.Min, rule.RefreshSeconds.Max),
            });
            return;
        }

        matchingEntries[0].Seconds = new MinMax<int>(rule.RefreshSeconds.Min, rule.RefreshSeconds.Max);
        foreach (var duplicate in matchingEntries.Skip(1))
        {
            traderConfig.UpdateTime.Remove(duplicate);
        }
    }

    private TraderPoolState CapturePool(TraderRule rule, Trader trader)
    {
        var sourceAssort = cloner.Clone(trader.Assort)
            // Internal invariant: caught at the rule boundary before any live assort is replaced.
            ?? throw new InvalidOperationException("Could not deep-clone the trader source assort.");

        if (sourceAssort.Items is null || sourceAssort.Items.Count == 0)
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("Trader source assort is empty.");
        }

        EnsureUniqueItemIds(sourceAssort.Items);
        var roots = FindAndValidateRoots(sourceAssort.Items);
        var itemById = sourceAssort.Items.ToDictionary(item => item.Id);
        var rootIds = roots.Select(root => root.Id).ToHashSet();
        EnsureOverridesTargetRoots(rule, rootIds);
        EnsureEveryChildHasAParent(sourceAssort.Items, itemById, rootIds, rule);

        var childrenByParent = sourceAssort.Items
            .Where(item => !rootIds.Contains(item.Id))
            .GroupBy(item => item.ParentId!)
            .ToDictionary(group => group.Key, group => group.ToList());

        var staticOffers = new List<WeightedOfferGroup>();
        var dynamicOffers = new List<WeightedOfferGroup>();
        var groupedItemIds = new HashSet<MongoId>();

        foreach (var root in roots)
        {
            var offerItems = CollectOfferItems(root, childrenByParent);
            foreach (var item in offerItems)
            {
                if (!groupedItemIds.Add(item.Id))
                {
                    // Mod-developer validation: caught at the rule boundary and disables only this rule.
                    throw new InvalidOperationException($"Item '{item.Id}' belongs to more than one root offer.");
                }
            }

            if (!sourceAssort.BarterScheme.TryGetValue(root.Id, out var barterSchemes))
            {
                // Mod-developer validation: caught at the rule boundary and disables only this rule.
                throw new InvalidOperationException($"Root '{root.Id}' has no barter scheme.");
            }

            if (!sourceAssort.LoyalLevelItems.TryGetValue(root.Id, out var loyaltyLevel))
            {
                // Mod-developer validation: caught at the rule boundary and disables only this rule.
                throw new InvalidOperationException($"Root '{root.Id}' has no loyalty level.");
            }

            var weight = weightReader.ReadWeight(root, rule);
            var offer = new WeightedOfferGroup
            {
                RootId = root.Id,
                Weight = weight,
                Items = offerItems,
                BarterSchemes = barterSchemes,
                LoyaltyLevel = loyaltyLevel,
            };

            if (weight == 0)
            {
                staticOffers.Add(offer);
                continue;
            }

            if (loyaltyLevel != 1)
            {
                // Mod-developer validation: caught at the rule boundary and disables only this rule.
                throw new InvalidOperationException($"Dynamic root '{root.Id}' must have loyalty level 1.");
            }

            if (trader.QuestAssort.Values.Any(mapping => mapping.ContainsKey(root.Id)))
            {
                // Mod-developer validation: caught at the rule boundary and disables only this rule.
                throw new InvalidOperationException($"Dynamic root '{root.Id}' must not be quest-locked.");
            }

            dynamicOffers.Add(offer);
        }

        if (groupedItemIds.Count != sourceAssort.Items.Count)
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("The source assort contains orphaned or cyclic child items.");
        }

        if (rule.DynamicOfferCount < 0)
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("dynamicOfferCount cannot be negative.");
        }

        if (rule.DynamicOfferCount > 0 && dynamicOffers.Count == 0)
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("dynamicOfferCount is positive but the source pool has no dynamic roots.");
        }

        if (dynamicOffers.Count < rule.DynamicOfferCount)
        {
            logger.Warning(
                $"Trader '{trader.Base.Id}' has only {dynamicOffers.Count} dynamic root(s); every dynamic offer will be active."
            );
        }

        return new TraderPoolState
        {
            Rule = rule,
            SourceAssort = sourceAssort,
            StaticOffers = staticOffers,
            DynamicOffers = dynamicOffers,
        };
    }

    private static void EnsureUniqueItemIds(IEnumerable<Item> items)
    {
        var duplicateId = items.GroupBy(item => item.Id).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException($"Source assort contains duplicate item ID '{duplicateId}'.");
        }
    }

    private static void EnsureOverridesTargetRoots(TraderRule rule, IReadOnlySet<MongoId> rootIds)
    {
        foreach (var rootId in rule.WeightOverrides.Keys)
        {
            if (!rootIds.Any(id => string.Equals(id.ToString(), rootId, StringComparison.Ordinal)))
            {
                // Mod-developer validation: caught at the rule boundary and disables only this rule.
                throw new InvalidOperationException($"Weight override targets unknown root '{rootId}'.");
            }
        }
    }

    private static List<Item> FindAndValidateRoots(IEnumerable<Item> items)
    {
        var roots = new List<Item>();
        foreach (var item in items)
        {
            var hasHideoutParent = string.Equals(item.ParentId, Hideout, StringComparison.OrdinalIgnoreCase);
            var hasHideoutSlot = string.Equals(item.SlotId, Hideout, StringComparison.OrdinalIgnoreCase);
            if (hasHideoutParent != hasHideoutSlot)
            {
                // Mod-developer validation: caught at the rule boundary and disables only this rule.
                throw new InvalidOperationException($"Item '{item.Id}' has malformed root markers.");
            }

            if (hasHideoutParent)
            {
                roots.Add(item);
            }
        }

        if (roots.Count == 0)
        {
            // Mod-developer validation: caught at the rule boundary and disables only this rule.
            throw new InvalidOperationException("Source assort contains no root offers.");
        }

        return roots;
    }

    private void EnsureEveryChildHasAParent(
        IEnumerable<Item> items,
        IReadOnlyDictionary<MongoId, Item> itemById,
        IReadOnlySet<MongoId> rootIds,
        TraderRule rule
    )
    {
        foreach (var item in items.Where(item => !rootIds.Contains(item.Id)))
        {
            if (string.IsNullOrWhiteSpace(item.ParentId)
                || !itemById.Keys.Any(id => string.Equals(id.ToString(), item.ParentId, StringComparison.Ordinal)))
            {
                // Mod-developer validation: caught at the rule boundary and disables only this rule.
                throw new InvalidOperationException($"Child item '{item.Id}' has no valid parent.");
            }

            if (weightReader.HasWeightProperty(item, rule.WeightProperty))
            {
                // Mod-developer validation: caught at the rule boundary and disables only this rule.
                throw new InvalidOperationException(
                    $"Weight property '{rule.WeightProperty}' is only valid on root offers; found it on child '{item.Id}'."
                );
            }
        }
    }

    private static List<Item> CollectOfferItems(Item root, IReadOnlyDictionary<string, List<Item>> childrenByParent)
    {
        var result = new List<Item>();
        var visited = new HashSet<MongoId>();
        var pending = new Stack<Item>();
        pending.Push(root);

        while (pending.TryPop(out var item))
        {
            if (!visited.Add(item.Id))
            {
                // Mod-developer validation: caught at the rule boundary and disables only this rule.
                throw new InvalidOperationException($"Offer rooted at '{root.Id}' contains a cyclic child graph.");
            }

            result.Add(item);
            if (childrenByParent.TryGetValue(item.Id.ToString(), out var children))
            {
                foreach (var child in children)
                {
                    pending.Push(child);
                }
            }
        }

        return result;
    }

    private void InstallRotation(Trader trader, TraderPoolState state, bool isRefresh)
    {
        var selectedDynamicOffers = SelectDynamicOffers(state, isRefresh);
        var activeOffers = state.StaticOffers.Concat(selectedDynamicOffers).ToList();
        var activeAssort = BuildActiveAssort(trader.Assort.NextResupply, activeOffers, state.Rule.WeightProperty);

        // A whole-object assignment ensures requests never observe mismatched assort collections.
        trader.Assort = activeAssort;
        state.CurrentDynamicRootIds = selectedDynamicOffers.Select(offer => offer.RootId).ToHashSet();
        trader.Base.RefreshTraderRagfairOffers = true;
    }

    private List<WeightedOfferGroup> SelectDynamicOffers(TraderPoolState state, bool isRefresh)
    {
        var selectionCount = Math.Min(state.Rule.DynamicOfferCount, state.DynamicOffers.Count);
        if (selectionCount == 0)
        {
            return [];
        }

        var candidates = state.DynamicOffers.ToList();
        var selected = new List<WeightedOfferGroup>(selectionCount);
        for (var index = 0; index < selectionCount; index++)
        {
            var next = DrawWeightedOffer(candidates);
            selected.Add(next);
            candidates.Remove(next);
        }

        if (isRefresh
            && state.Rule.GuaranteeDifferentRotation
            && state.DynamicOffers.Count > selectionCount
            && selected.Select(offer => offer.RootId).ToHashSet().SetEquals(state.CurrentDynamicRootIds))
        {
            var replacementIndex = randomUtil.GetInt(0, selected.Count, exclusive: true);
            var outsideOffers = state.DynamicOffers
                .Where(offer => !selected.Any(selectedOffer => selectedOffer.RootId == offer.RootId))
                .ToList();

            selected[replacementIndex] = DrawWeightedOffer(outsideOffers);
        }

        return selected;
    }

    private WeightedOfferGroup DrawWeightedOffer(IReadOnlyCollection<WeightedOfferGroup> candidates)
    {
        var maximumWeight = candidates.Max(candidate => candidate.Weight);
        var totalNormalizedWeight = candidates.Sum(candidate => candidate.Weight / maximumWeight);
        var roll = randomUtil.GetDouble(0, totalNormalizedWeight);
        var cumulativeWeight = 0d;

        foreach (var candidate in candidates)
        {
            cumulativeWeight += candidate.Weight / maximumWeight;
            if (roll < cumulativeWeight)
            {
                return candidate;
            }
        }

        return candidates.Last();
    }

    private TraderAssort BuildActiveAssort(
        double? nextResupply,
        IEnumerable<WeightedOfferGroup> activeOffers,
        string weightProperty
    )
    {
        var items = new List<Item>();
        var barterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>();
        var loyalLevelItems = new Dictionary<MongoId, int>();

        foreach (var offer in activeOffers)
        {
            var clonedItems = cloner.Clone(offer.Items)
                // Internal invariant: caught before an incomplete active assort can be assigned.
                ?? throw new InvalidOperationException($"Could not clone items for root '{offer.RootId}'.");
            var clonedBarterSchemes = cloner.Clone(offer.BarterSchemes)
                // Internal invariant: caught before an incomplete active assort can be assigned.
                ?? throw new InvalidOperationException($"Could not clone barter scheme for root '{offer.RootId}'.");

            var clonedRoot = clonedItems.SingleOrDefault(item => item.Id == offer.RootId)
                // Internal invariant: caught before an incomplete active assort can be assigned.
                ?? throw new InvalidOperationException($"Cloned offer '{offer.RootId}' has no root item.");
            if (clonedRoot.TryGetExtensionData(out var extensionData))
            {
                extensionData!.Remove(weightProperty);
            }

            items.AddRange(clonedItems);
            barterScheme.Add(offer.RootId, clonedBarterSchemes);
            loyalLevelItems.Add(offer.RootId, offer.LoyaltyLevel);
        }

        ValidateActiveAssort(items, barterScheme, loyalLevelItems);
        return new TraderAssort
        {
            NextResupply = nextResupply,
            Items = items,
            BarterScheme = barterScheme,
            LoyalLevelItems = loyalLevelItems,
        };
    }

    private static void ValidateActiveAssort(
        IEnumerable<Item> items,
        IReadOnlyDictionary<MongoId, List<List<BarterScheme>>> barterScheme,
        IReadOnlyDictionary<MongoId, int> loyalLevelItems
    )
    {
        var materializedItems = items.ToList();
        var itemIds = materializedItems.Select(item => item.Id).ToHashSet();
        var roots = materializedItems.Where(item => string.Equals(item.SlotId, Hideout, StringComparison.OrdinalIgnoreCase));

        foreach (var root in roots)
        {
            if (!barterScheme.ContainsKey(root.Id) || !loyalLevelItems.ContainsKey(root.Id))
            {
                // Internal invariant: caught before an incomplete active assort can be assigned.
                throw new InvalidOperationException($"Active root '{root.Id}' is missing barter or loyalty data.");
            }
        }

        foreach (var child in materializedItems.Where(item => !string.Equals(item.ParentId, Hideout, StringComparison.OrdinalIgnoreCase)))
        {
            if (child.ParentId is null
                || !itemIds.Any(id => string.Equals(id.ToString(), child.ParentId, StringComparison.Ordinal)))
            {
                // Internal invariant: caught before an incomplete active assort can be assigned.
                throw new InvalidOperationException($"Active child '{child.Id}' refers to an item outside the active assort.");
            }
        }
    }
}
