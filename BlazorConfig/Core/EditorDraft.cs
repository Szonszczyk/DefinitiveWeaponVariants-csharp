using System.Text.Json.Nodes;

namespace ModConfigEditor.Core;

/// <summary>
/// One browser's working copy. Nothing here refers to the live mod config or DI.
/// Object editors recurse through the same class, including their unknown keys.
/// </summary>
public sealed class ObjectDraft
{
    private readonly HashSet<string> _reservedNames;
    public List<FieldDraft> Fields { get; } = [];
    public List<RawEntry> Unknown { get; } = [];

    public ObjectDraft(IEnumerable<PropertyDefinition> definitions, JsonObject current, JsonObject defaults)
    {
        var properties = definitions.ToList();
        _reservedNames = properties.SelectMany(p => new[] { p.Property }.Concat(p.RenamedFrom ?? []))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var definition in properties)
        {
            // Presence matters: explicit null is invalid input, not a missing key.
            var source = new[] { definition.Property }.Concat(definition.RenamedFrom ?? [])
                .FirstOrDefault(current.ContainsKey);
            var fallback = definition.Default ?? defaults[definition.Property];
            var value = source is null ? fallback : current[source];
            Fields.Add(new FieldDraft(definition, value?.DeepClone(), fallback?.DeepClone(),
                source is null ? "Missing setting: the default will be added on Save."
                : source != definition.Property ? $"Loaded from {source}; Save migrates to {definition.Property}."
                : (definition.RenamedFrom ?? []).Any(current.ContainsKey) ? "Current name wins; Save removes legacy aliases." : null));
        }
        foreach (var pair in current.Where(pair => !_reservedNames.Contains(pair.Key)))
            Unknown.Add(new RawEntry(pair.Key, JsonValues.Format(pair.Value)));
    }

    public bool IsReserved(string name) => _reservedNames.Contains(name);

    public JsonObject Build(string path = "config")
    {
        // Build a NEW tree. Alias removal and edits are only committed if every
        // field validates and the file store subsequently completes its write.
        var result = new JsonObject();
        foreach (var field in Fields)
            result.Add(field.Definition.Property, field.Build($"{path}.{field.Definition.Property}"));
        foreach (var entry in Unknown)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) || IsReserved(entry.Name) || result.ContainsKey(entry.Name))
                throw new InvalidDataException($"{path}: unknown property '{entry.Name}' is empty, duplicated, or reserved by the schema.");
            try { result.Add(entry.Name, JsonValues.Parse(entry.Text)); }
            catch (System.Text.Json.JsonException ex) { throw new InvalidDataException($"{path}.{entry.Name}: invalid JSON. {ex.Message}"); }
        }
        return result;
    }
}

public sealed class FieldDraft
{
    private readonly JsonNode? _default;
    public PropertyDefinition Definition { get; }
    public string? Notice { get; }
    public string Text { get; set; } = "";
    public ObjectDraft? Object { get; private set; }
    public List<RawEntry> Entries { get; } = [];
    public List<bool> Selected { get; } = [];
    public int SelectedIndex { get; set; } = -1;
    public string? Error { get; private set; }
    public bool RawMode { get; private set; }
    public bool BoolValue { get => Text == "true"; set => Text = value ? "true" : "false"; }

    public FieldDraft(PropertyDefinition definition, JsonNode? value, JsonNode? fallback, string? notice)
    {
        Definition = definition;
        _default = fallback?.DeepClone();
        Notice = notice;
        Load(value);
    }

    // Reset is explicit and affects this draft only. A malformed existing value
    // stays visible in raw mode until the user repairs it or chooses this action.
    public void Reset() => Load(_default?.DeepClone());

    public void UseRawValue()
    {
        try { Load(JsonValues.Parse(Text)); }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException)
        { Error = ex.Message; }
    }

    private void Load(JsonNode? value)
    {
        Text = JsonValues.Format(value);
        Object = null;
        Entries.Clear();
        Selected.Clear();
        SelectedIndex = -1;
        Error = null;
        RawMode = false;
        try
        {
            switch (Definition.Type)
            {
                case "object":
                    // Missing objects can be assembled from child defaults, but
                    // an explicit scalar/null must not silently destroy user data.
                    if (value is null && Notice?.StartsWith("Missing setting") == true) value = new JsonObject();
                    if (value is not JsonObject obj) throw new InvalidDataException("Expected an object. Repair the JSON or reset this field.");
                    Object = new ObjectDraft(Definition.Properties ?? [], obj, _default as JsonObject ?? new());
                    break;
                case "dictionary":
                    if (value is not JsonObject dictionary) throw new InvalidDataException("Expected a dictionary object.");
                    foreach (var pair in dictionary)
                    {
                        JsonValues.ValidateScalar(pair.Value, Definition.ValueType!, Definition);
                        Entries.Add(new RawEntry(pair.Key, ScalarText(pair.Value, Definition.ValueType!)));
                    }
                    break;
                case "list":
                    var options = Definition.AllowedValues;
                    if (options is not null && !Definition.AllowMultiple)
                    {
                        // Single-choice lists store one scalar, not a JSON array.
                        SelectedIndex = options.FindIndex(option => JsonNode.DeepEquals(option, value));
                        if (SelectedIndex < 0) throw new InvalidDataException("Current value is not an allowed choice.");
                    }
                    else
                    {
                        if (value is not JsonArray array) throw new InvalidDataException("Expected a JSON array.");
                        foreach (var item in array) JsonValues.ValidateScalar(item, Definition.ItemType ?? "string", Definition);
                        if (options is not null)
                        {
                            if (array.Any(item => !options.Any(option => JsonNode.DeepEquals(item, option))))
                                throw new InvalidDataException("The array contains a value outside AllowedValues.");
                            if (array.Select(JsonValues.Format).Distinct().Count() != array.Count)
                                throw new InvalidDataException("Multi-select values must be unique.");
                            Selected.AddRange(options.Select(option => array.Any(item => JsonNode.DeepEquals(item, option))));
                        }
                        else foreach (var item in array) Entries.Add(new RawEntry("", ScalarText(item, Definition.ItemType ?? "string")));
                    }
                    break;
                default:
                    JsonValues.ValidateScalar(value, Definition.Type, Definition);
                    Text = ScalarText(value, Definition.Type);
                    break;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or OverflowException)
        {
            RawMode = true;
            Text = JsonValues.Format(value);
            Error = ex.Message;
        }
    }

    public JsonNode? Build(string path)
    {
        Error = null;
        try
        {
            if (RawMode) throw new InvalidDataException("Repair the raw JSON and click Use repaired value, or reset the field, before saving.");
            switch (Definition.Type)
            {
                case "object": return Object!.Build(path);
                case "dictionary":
                    var dictionary = new JsonObject();
                    foreach (var entry in Entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Name) || dictionary.ContainsKey(entry.Name))
                            throw new InvalidDataException("Dictionary keys must be nonempty and unique.");
                        dictionary.Add(entry.Name, JsonValues.ScalarFromText(entry.Text, Definition.ValueType!, Definition));
                    }
                    return dictionary;
                case "list":
                    if (Definition.AllowedValues is { } options)
                    {
                        if (!Definition.AllowMultiple)
                        {
                            if (SelectedIndex < 0 || SelectedIndex >= options.Count) throw new InvalidDataException("Choose a value.");
                            return options[SelectedIndex]?.DeepClone();
                        }
                        return new JsonArray(options.Where((_, i) => Selected[i]).Select(v => v?.DeepClone()).ToArray());
                    }
                    return new JsonArray(Entries.Select(e => JsonValues.ScalarFromText(e.Text, Definition.ItemType ?? "string", Definition)).ToArray());
                default: return JsonValues.ScalarFromText(Text, Definition.Type, Definition);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or FormatException or OverflowException)
        {
            Error = ex.Message;
            throw new InvalidDataException($"{path}: {ex.Message}");
        }
    }

    private static string ScalarText(JsonNode? value, string type) => type == "string" ? value!.GetValue<string>() : JsonValues.Format(value);
    public string NewEntryText => (Definition.ValueType ?? Definition.ItemType ?? "string") switch { "bool" => "false", "int" or "float" => "0", _ => "" };
}

// Lists deliberately use objects as their row identities. Blazor's @key keeps a
// focused input attached to its own row when a preceding row is removed.
public sealed class RawEntry(string name, string text)
{
    public string Name { get; set; } = name;
    public string Text { get; set; } = text;
}
