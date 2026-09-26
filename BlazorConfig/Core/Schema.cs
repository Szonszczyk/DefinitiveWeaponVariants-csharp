using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModConfigEditor.Core;

// This is the same schema vocabulary as Config File Configurator. It is a UI
// description, not the standard JSON Schema specification. Keep JSON key casing.
public sealed class ConfigSchema
{
    public List<SchemaSection> Sections { get; init; } = [];
    public IEnumerable<PropertyDefinition> RootProperties => Sections.SelectMany(s => s.Properties);

    public void Validate()
    {
        if (Sections is null || Sections.Any(s => s is null || s.Properties is null))
            throw new InvalidDataException("Schema sections and property lists cannot be null.");
        ValidateLevel(RootProperties, "config");
    }

    private static void ValidateLevel(IEnumerable<PropertyDefinition> definitions, string path)
    {
        // Current names and aliases share one namespace at each object level.
        // Reject overlaps up front: otherwise saving one field could erase another.
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (definition is null) throw new InvalidDataException($"Null property at {path}.");
            foreach (var name in new[] { definition.Property }.Concat(definition.RenamedFrom ?? []))
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                    throw new InvalidDataException($"Empty or overlapping schema name '{name}' at {path}.");
            if (definition.Type is not ("bool" or "int" or "float" or "string" or "list" or "object" or "dictionary"))
                throw new InvalidDataException($"Unsupported type '{definition.Type}' at {path}.{definition.Property}.");
            if (definition.Minimum > definition.Maximum)
                throw new InvalidDataException($"Minimum exceeds maximum for {definition.Property}.");
            if (definition.Type == "dictionary" && definition.ValueType is not ("bool" or "int" or "float" or "string"))
                throw new InvalidDataException($"Invalid dictionary ValueType for {definition.Property}.");
            if (definition.Type == "list" && (definition.ItemType ?? "string") is not ("bool" or "int" or "float" or "string"))
                throw new InvalidDataException($"Invalid list ItemType for {definition.Property}.");
            if (definition.AllowedValues is { } options)
                foreach (var option in options)
                    JsonValues.ValidateScalar(option, definition.ItemType ?? "string", definition);
            if (definition.Type == "object") ValidateLevel(definition.Properties ?? [], $"{path}.{definition.Property}");
        }
    }
}

public sealed class SchemaSection
{
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Description { get; init; }
    public List<PropertyDefinition> Properties { get; init; } = [];
}

public sealed class PropertyDefinition
{
    public string Property { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Description { get; init; }
    public string Type { get; init; } = "string";
    public JsonNode? Default { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public string? ItemType { get; init; }
    public string? ValueType { get; init; }
    public List<JsonNode?>? AllowedValues { get; init; }
    public bool AllowMultiple { get; init; }
    public List<string>? RenamedFrom { get; init; }
    public List<PropertyDefinition>? Properties { get; init; }
    public string Label => string.IsNullOrWhiteSpace(DisplayName) ? Property : DisplayName;
}

public static class JsonValues
{
    public static readonly JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static JsonNode? Parse(string text) => JsonNode.Parse(text, documentOptions: new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    });

    public static JsonObject ParseObject(string text) => Parse(text) as JsonObject
        ?? throw new InvalidDataException("The configuration must be a JSON object.");

    public static string Format(JsonNode? value) => value?.ToJsonString(Options) ?? "null";

    // Keep text as text until Save. In particular, never round a fractional int,
    // silently clamp an out-of-range number, or replace malformed input with zero.
    public static JsonNode ScalarFromText(string text, string type, PropertyDefinition definition)
    {
        JsonNode? value = type == "string" ? JsonValue.Create(text) : Parse(text);
        ValidateScalar(value, type, definition);
        return value!;
    }

    public static void ValidateScalar(JsonNode? value, string type, PropertyDefinition definition)
    {
        var kind = value?.GetValueKind();
        var valid = type switch
        {
            "string" => kind == JsonValueKind.String,
            "bool" => kind is JsonValueKind.True or JsonValueKind.False,
            "int" => kind == JsonValueKind.Number && long.TryParse(value!.ToJsonString(), out _),
            "float" => kind == JsonValueKind.Number,
            _ => false
        };
        if (!valid) throw new InvalidDataException($"Expected {type}.");
        if (type is "int" or "float")
        {
            var number = double.Parse(value!.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
            if (!double.IsFinite(number)) throw new InvalidDataException("Enter a finite number.");
            if (number < definition.Minimum || number > definition.Maximum)
                throw new InvalidDataException($"Value must be between {definition.Minimum?.ToString() ?? "no minimum"} and {definition.Maximum?.ToString() ?? "no maximum"}.");
        }
    }
}
