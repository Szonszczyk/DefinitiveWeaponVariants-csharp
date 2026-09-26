using System.Text.Json.Nodes;

namespace ModConfigEditor.Core;

/// <summary>
/// Counts pending settings, not keystrokes. Capture UI state without calling
/// Build(), which would validate incomplete input and modify field error messages.
/// The snapshot is independent of draft objects, so Reset can replace a whole
/// subtree without accidentally resetting that subtree's comparison baseline.
/// </summary>
public sealed class DraftChangeTracker(ObjectDraft draft)
{
    private readonly Snapshot _baseline = CaptureObject(draft);
    public int CountChanges() => Count(_baseline, CaptureObject(draft));

    private sealed record Snapshot(JsonNode? Value, Dictionary<string, Snapshot>? Children = null);

    private static Snapshot CaptureObject(ObjectDraft obj)
    {
        var children = obj.Fields.ToDictionary(f => "field:" + f.Definition.Property, CaptureField, StringComparer.Ordinal);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in obj.Unknown)
        {
            // Include occurrences so unfinished duplicate/empty keys still count
            // and cannot crash the counter. Names are length-delimited to avoid
            // ambiguity with names containing colons or other punctuation.
            var occurrence = occurrences.GetValueOrDefault(entry.Name);
            occurrences[entry.Name] = occurrence + 1;
            children.Add($"unknown:{entry.Name.Length}:{entry.Name}:{occurrence}", new(ParseRaw(entry.Text)));
        }
        return new(null, children);
    }

    private static Snapshot CaptureField(FieldDraft field)
    {
        if (field.RawMode) return new(new JsonObject { ["raw"] = ParseRaw(field.Text) });
        if (field.Object is { } obj) return CaptureObject(obj);
        if (field.Definition.AllowedValues is not null && field.Definition.Type == "list")
            return new(field.Definition.AllowMultiple
                ? new JsonArray(field.Selected.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray())
                : JsonValue.Create(field.SelectedIndex));
        if (field.Definition.Type is "dictionary" or "list")
            return new(new JsonArray(field.Entries.Select(e => (JsonNode?)new JsonObject { ["name"] = e.Name, ["text"] = e.Text }).ToArray()));
        return new(JsonValue.Create(field.Text));
    }

    // Whitespace-only changes to valid raw JSON are not a pending value change.
    // Invalid JSON still has a stable snapshot while the player is typing it.
    private static JsonNode ParseRaw(string text)
    {
        try { return new JsonObject { ["json"] = JsonValues.Parse(text) }; }
        catch (System.Text.Json.JsonException) { return new JsonObject { ["invalid"] = text }; }
    }

    private static int Count(Snapshot before, Snapshot after)
    {
        if (before.Children is not { } original || after.Children is not { } current)
            return before.Children is null && after.Children is null && JsonNode.DeepEquals(before.Value, after.Value) ? 0 : 1;
        return original.Keys.Union(current.Keys, StringComparer.Ordinal).Sum(key =>
            !original.TryGetValue(key, out var old) || !current.TryGetValue(key, out var edited) ? 1 : Count(old, edited));
    }
}
