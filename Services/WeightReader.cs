using System.Text.Json;
using DefinitiveWeaponVariants.Models;
using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace DefinitiveWeaponVariants.Services;

[Injectable(InjectionType.Singleton)]
public sealed class WeightReader
{
    public bool HasWeightProperty(Item item, string propertyName)
    {
        return item.TryGetExtensionData(out var extensionData)
            && extensionData!.ContainsKey(propertyName);
    }

    public double ReadWeight(Item rootItem, TraderRule rule)
    {
        var rootId = rootItem.Id.ToString();
        if (rule.WeightOverrides.TryGetValue(rootId, out var overrideWeight))
        {
            return ValidateWeight(overrideWeight, $"override for root '{rootId}'");
        }

        if (rootItem.TryGetExtensionData(out var extensionData)
            && extensionData!.TryGetValue(rule.WeightProperty, out var rawWeight)
            && TryConvertToDouble(rawWeight, out var parsedWeight))
        {
            return ValidateWeight(parsedWeight, $"property '{rule.WeightProperty}' on root '{rootId}'");
        }

        return rule.MissingWeightBehavior.ToLowerInvariant() switch
        {
            "static" => 0,
            "dynamicdefault" => ValidateWeight(
                rule.DefaultDynamicWeight,
                $"default dynamic weight for root '{rootId}'"
            ),
            // Mod-developer validation: initialization catches this and disables only this trader rule.
            "error" => throw new InvalidOperationException(
                $"Root '{rootId}' is missing a valid '{rule.WeightProperty}' weight."
            ),
            // Mod-developer validation: initialization catches this and disables only this trader rule.
            _ => throw new InvalidOperationException(
                $"Unknown missingWeightBehavior '{rule.MissingWeightBehavior}' for root '{rootId}'."
            ),
        };
    }

    private static double ValidateWeight(double value, string source)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            // Mod-developer validation: initialization catches this and disables only this trader rule.
            throw new InvalidOperationException($"Weight from {source} must be a finite value greater than or equal to zero.");
        }

        return value;
    }

    private static bool TryConvertToDouble(object value, out double result)
    {
        switch (value)
        {
            case byte number:
                result = number;
                return true;
            case sbyte number:
                result = number;
                return true;
            case short number:
                result = number;
                return true;
            case ushort number:
                result = number;
                return true;
            case int number:
                result = number;
                return true;
            case uint number:
                result = number;
                return true;
            case long number:
                result = number;
                return true;
            case ulong number:
                result = number;
                return true;
            case float number:
                result = number;
                return true;
            case double number:
                result = number;
                return true;
            case decimal number:
                result = (double)number;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } jsonNumber when jsonNumber.TryGetDouble(out result):
                return true;
            default:
                result = default;
                return false;
        }
    }
}
