using DefinitiveWeaponVariants.Interfaces;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace DefinitiveWeaponVariants.CustomClasses;

[Injectable(InjectionType.Singleton)]
public class CustomPropertiesChanger(
    ISptLogger<DefinitiveWeaponVariants> logger
)
{
    public TemplateItemProperties ChangeItemProperties(
        Dictionary<string, object> newProperties,
        TemplateItemProperties overrideProperties,
        TemplateItem copiedItem,
        VariantConfiguration config,
        string variantShortName
    )
    {
        foreach (var (propertyName, newPropertyValue) in newProperties)
        {
            if (copiedItem.Properties == null) continue;

            var originalProp = copiedItem.Properties.GetType().GetProperty(propertyName);

            if (originalProp == null)
            {
                logger.LogWithColor($"[{GetType().Namespace}] Incorrect property '{propertyName}' in {variantShortName} variant!", LogTextColor.Red);
                continue;
            }

            JsonElement jsonPropertyValue = newPropertyValue is string ? JsonDocument.Parse($"\"{newPropertyValue}\"").RootElement : (JsonElement)newPropertyValue;

            switch (originalProp.GetValue(copiedItem.Properties))
            {
                case double d:
                    if (config?.Changes?.Minimum != null)
                    {
                        foreach (var change in config.Changes.Minimum)
                        {
                            if (change.Key == propertyName && d < change.Value) d = change.Value;
                        }
                    }
                    if (jsonPropertyValue.ValueKind == JsonValueKind.Number || jsonPropertyValue.ValueKind == JsonValueKind.String)
                        TrySetProperty(overrideProperties, propertyName, CalculateValue(d, jsonPropertyValue));
                    else
                        logger.LogWithColor($"[{GetType().Namespace}] Value for '{variantShortName}' property '{propertyName}' is of incorrect type. Expected 'double/string', got '{newPropertyValue}'({jsonPropertyValue.ValueKind})!", LogTextColor.Red);
                    break;
                case int i:
                    if (config?.Changes?.Minimum != null)
                    {
                        foreach (var change in config.Changes.Minimum)
                        {
                            if (change.Key == propertyName && i < change.Value) i = (int)change.Value;
                        }
                    }
                    if (jsonPropertyValue.ValueKind == JsonValueKind.Number || jsonPropertyValue.ValueKind == JsonValueKind.String)
                        TrySetProperty(overrideProperties, propertyName, (int)CalculateValue(i, jsonPropertyValue));
                    else
                        logger.LogWithColor($"[{GetType().Namespace}] Value for '{variantShortName}' property '{propertyName}' is of incorrect type. Expected 'int/string', got '{newPropertyValue}'({jsonPropertyValue.ValueKind})!", LogTextColor.Red);
                    break;
                case bool b:
                    if (jsonPropertyValue.ValueKind == JsonValueKind.False || jsonPropertyValue.ValueKind == JsonValueKind.True)
                        TrySetProperty(overrideProperties, propertyName, newPropertyValue);
                    else
                        logger.LogWithColor($"[{GetType().Namespace}] Value for '{variantShortName}' property '{propertyName}' is of incorrect type. Expected 'bool', got '{newPropertyValue}'({jsonPropertyValue.ValueKind})!", LogTextColor.Red);
                    break;
                case string s:
                    if (jsonPropertyValue.ValueKind == JsonValueKind.String)
                        TrySetProperty(overrideProperties, propertyName, newPropertyValue);
                    else
                        logger.LogWithColor($"[{GetType().Namespace}] Value for '{variantShortName}' property '{propertyName}' is of incorrect type. Expected 'string', got '{newPropertyValue}'({jsonPropertyValue.ValueKind})!", LogTextColor.Red);
                    break;
                case MongoId id:
                    string? mongoIdString = jsonPropertyValue.GetString();
                    if (jsonPropertyValue.ValueKind == JsonValueKind.String && mongoIdString is not null && MongoId.IsValidMongoId(mongoIdString))
                        TrySetProperty(overrideProperties, propertyName, newPropertyValue);
                    else
                        logger.LogWithColor($"[{GetType().Namespace}] Value for '{variantShortName}' property '{propertyName}' is of incorrect type. Expected 'MongoId', got '{newPropertyValue}'({jsonPropertyValue.ValueKind})!", LogTextColor.Red);
                    break;
                case HashSet<string> hSet:
                    if (jsonPropertyValue.ValueKind == JsonValueKind.Array)
                        TrySetProperty(overrideProperties, propertyName, newPropertyValue);
                    else
                        logger.LogWithColor($"[{GetType().Namespace}] Value for '{variantShortName}' property '{propertyName}' is of incorrect type. Expected 'array<string>', got '{newPropertyValue.ToString}'({jsonPropertyValue.ValueKind})!", LogTextColor.Red);
                    break;
                case null:
                    break;
                default:
                    break;
            }
        }
        return overrideProperties;
    }

    private void TrySetProperty(object target, string propertyName, object? value)
    {
        if (target == null) return;

        var type = target.GetType();
        var prop = type.GetProperty(propertyName);

        if (prop == null)
        {
            logger.LogWithColor($"[{GetType().Namespace}] Property '{propertyName}' not found on type {type.Name}", LogTextColor.Red);
            return;
        }

        if (!prop.CanWrite)
        {
            logger.LogWithColor($"[{GetType().Namespace}] Property '{propertyName}' is read-only!", LogTextColor.Red);
            return;
        }

        try
        {
            var propType = prop.PropertyType;

            if (propType == typeof(MongoId?))
            {
                try
                {
                    string? idString = null;

                    if (value is JsonElement je)
                        idString = je.GetString();
                    else
                        idString = value?.ToString();

                    if (string.IsNullOrWhiteSpace(idString))
                    {
                        logger.LogWithColor($"[{GetType().Namespace}] MongoId value for '{propertyName}' is null or empty!", LogTextColor.Red);
                        return;
                    }

                    value = new MongoId(idString);
                    prop.SetValue(target, value);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Failed to convert value to MongoId for '{propertyName}': {ex.Message}", LogTextColor.Red);
                    return;
                }
            }
            if (value is JsonElement jsonElement)
            {
                try
                {
                    value = jsonElement.Deserialize(propType, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Failed to deserialize JsonElement to {propType.Name}: {ex.Message}", LogTextColor.Red);
                    return;
                }
            }
            else if (value != null && !propType.IsAssignableFrom(value.GetType()))
            {
                try
                {
                    value = Convert.ChangeType(value, propType);
                }
                catch
                {
                    logger.LogWithColor($"[{GetType().Namespace}] Cannot convert value of type {value.GetType().Name} to {propType.Name}", LogTextColor.Red);
                    return;
                }
            }
            prop.SetValue(target, value);
        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[{GetType().Namespace}] Error setting '{propertyName}': {ex.Message}", LogTextColor.Red);
            return;
        }
    }
    private static double CalculateValue(double first, JsonElement second)
    {
        if (second.ValueKind == JsonValueKind.String)
        {
            string str = second.GetString()!;
            if (Regex.IsMatch(str, @"^[+-]\d+%?$"))
            {
                bool isPercentage = str.EndsWith("%");
                if (double.TryParse(str.TrimEnd('%'), out double value))
                {
                    double result = isPercentage
                        ? first + (first * (value / 100))
                        : first + value;

                    return Math.Round(result, 3);
                }
                else return first;
            }
            else return first;
        }
        else if (second.ValueKind == JsonValueKind.Number)
            return second.GetDouble();
        else
            return first;
    }
}
