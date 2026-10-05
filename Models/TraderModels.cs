using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System;
using System.Collections.Generic;
using System.Text;

namespace DefinitiveWeaponVariants.Models;

/// <summary>
/// The JSON extension-data field used to classify a root trader offer.
/// </summary>
public static class RandomAssortWeight
{
    public const string PropertyName = "randomAssortWeight";
}

/// <summary>
/// Immutable source offers and transient rotation state for one configured trader.
/// </summary>
public sealed class TraderPoolState
{
    public required TraderRule Rule { get; init; }
    public required TraderAssort SourceAssort { get; init; }
    public required List<WeightedOfferGroup> StaticOffers { get; init; }
    public required List<WeightedOfferGroup> DynamicOffers { get; init; }
    public HashSet<MongoId> CurrentDynamicRootIds { get; set; } = [];
    public int? LastHandledExpiredTimestamp { get; set; }
}

/// <summary>
/// One selectable root offer and every descendant item that belongs to it.
/// </summary>
public sealed class WeightedOfferGroup
{
    public required MongoId RootId { get; init; }
    public required double Weight { get; init; }
    public required List<Item> Items { get; init; }
    public required List<List<BarterScheme>> BarterSchemes { get; init; }
    public required int LoyaltyLevel { get; init; }
}

public sealed class TraderDynamicAssortConfig
{
    public bool Enabled { get; set; } = true;
    public List<TraderRule> Traders { get; set; } = [];
}

public sealed class TraderRule
{
    public string TraderId { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int DynamicOfferCount { get; set; } = 15;
    public string WeightProperty { get; set; } = "randomAssortWeight";
    public Dictionary<string, double> WeightOverrides { get; set; } = [];
    // static - missing weight makes item static
    // dynamicdefault - missing weight gives an item DefaultDynamicWeight
    // error - throws error when missing weight
    public string MissingWeightBehavior { get; set; } = "static";
    public double DefaultDynamicWeight { get; set; } = 1;
    public bool GuaranteeDifferentRotation { get; set; } = true;
    public RefreshInterval RefreshSeconds { get; set; } = new();
    public bool StrictValidation { get; set; }
}

public sealed class RefreshInterval
{
    public int Min { get; set; } = 900;
    public int Max { get; set; } = 1800;
}

