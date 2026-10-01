using System.Text.Json;
using System.Text.Json.Nodes;
using ModConfigEditor.Core;

// These checks exercise real JSON and filesystem behavior, with no SPT process,
// test framework package, live installation, or real player configuration.
var passed = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
    passed++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (Exception ex) when (ex is InvalidDataException or JsonException) { Check(true, label); return; }
    throw new Exception("FAIL: should reject " + label);
}
async Task RejectSave(Func<Task> action, string label)
{
    try { await action(); }
    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { Check(true, label); return; }
    throw new Exception("FAIL: should reject " + label);
}
PropertyDefinition Definition(string name, string type, JsonNode? fallback = null) => new() { Property = name, Type = type, Default = fallback };

var nested = new PropertyDefinition { Property = "nested", Type = "object", RenamedFrom = ["oldNested"], Properties = [Definition("flag", "bool", JsonValue.Create(true))] };
var renamed = new PropertyDefinition { Property = "count", Type = "int", Default = JsonValue.Create(2), Minimum = 0, Maximum = 10, RenamedFrom = ["oldCount", "olderCount"] };
var schema = new ConfigSchema { Sections = [new() { Name = "DisplayGroup", Properties = [renamed, nested] }] };
schema.Validate();
var source = JsonValues.ParseObject("""{"oldCount":3,"olderCount":4,"oldNested":{"flag":false,"future":{"array":[1,null,{"x":true}]}},"futureRoot":["x",null]}""");
var draft = new ObjectDraft(schema.RootProperties, source, new());
var result = draft.Build();
Check(result["count"]!.GetValue<int>() == 3 && !result.ContainsKey("oldCount") && !result.ContainsKey("olderCount"), "ordered alias migration");
Check(JsonNode.DeepEquals(result["nested"]!["future"], source["oldNested"]!["future"]) && JsonNode.DeepEquals(result["futureRoot"], source["futureRoot"]), "unknown nested and root JSON survives migration");
Check(!result.ContainsKey("DisplayGroup") && source.ContainsKey("oldCount"), "sections do not create JSON nesting; draft leaves source untouched");
source["count"] = 7;
Check(new ObjectDraft(schema.RootProperties, source, new()).Build()["count"]!.GetValue<int>() == 7, "current property wins over aliases");
draft.Fields[0].Text = "1.5";
Reject(() => draft.Build(), "fractional integers rejected without rounding");
draft.Fields[0].Text = "11";
Reject(() => draft.Build(), "numeric range enforced");
draft.Fields[0].Text = "3";
draft.Unknown.Add(new("count", "12"));
Reject(() => draft.Build(), "unknown name cannot collide with known field");
draft.Unknown.RemoveAt(draft.Unknown.Count - 1);
draft.Unknown[0].Text = "{broken";
Reject(() => draft.Build(), "invalid raw unknown JSON rejected");
draft.Unknown[0].Text = "null";
Check(draft.Build().ContainsKey("futureRoot") && draft.Build()["futureRoot"] is null, "unknown explicit null preserved");
draft.Unknown.Clear();
draft.Fields[1].Object!.Unknown.Clear();
Check(!draft.Build().ContainsKey("futureRoot") && !((JsonObject)draft.Build()["nested"]!).ContainsKey("future"), "explicit unknown deletion works at both levels");

var malformed = new ObjectDraft([nested], JsonValues.ParseObject("""{"nested":42}"""), new());
Check(malformed.Fields[0].RawMode && malformed.Fields[0].Text == "42", "wrong object shape retained for explicit repair");
Reject(() => malformed.Build(), "wrong object shape cannot be silently discarded");
malformed.Fields[0].Text = "{\"flag\":true,\"keep\":[1,2]}";
malformed.Fields[0].UseRawValue();
Check(malformed.Build()["nested"]!["keep"] is JsonArray, "raw shape repair preserves unknowns");
var explicitNull = new ObjectDraft([Definition("flag", "bool", JsonValue.Create(true))], JsonValues.ParseObject("{\"flag\":null}"), new());
Reject(() => explicitNull.Build(), "explicit known null is not treated as missing");
Check(new ObjectDraft([nested], new(), new()).Build()["nested"]!["flag"]!.GetValue<bool>(), "missing nested object receives child defaults");

var free = new FieldDraft(new() { Property = "items", Type = "list", ItemType = "int" }, JsonValues.Parse("[1,2]"), null, null);
free.Entries.Add(new("", "3"));
Check(free.Build("items")!.ToJsonString() == "[1,2,3]", "typed free-form list saves array");
free.Entries[0].Text = "oops";
Reject(() => free.Build("items"), "typed list validation");
var single = new FieldDraft(new() { Property = "choice", Type = "list", AllowedValues = [JsonValue.Create("a"), JsonValue.Create("b")] }, JsonValue.Create("b"), null, null);
Check(single.Build("choice")!.ToJsonString() == "\"b\"", "single allowed choice saves scalar");
var multiDefinition = new PropertyDefinition { Property = "choices", Type = "list", AllowMultiple = true, AllowedValues = [JsonValue.Create("a"), JsonValue.Create("b")] };
var multi = new FieldDraft(multiDefinition, JsonValues.Parse("[\"b\"]"), null, null);
multi.Selected[0] = true;
Check(multi.Build("choices")!.ToJsonString() == "[\"a\",\"b\"]", "multi-choice saves selected array");
Reject(() => new FieldDraft(multiDefinition, JsonValues.Parse("[\"unknown\"]"), null, null).Build("choices"), "unknown allowed choice is not dropped");
var dict = new FieldDraft(new() { Property = "weights", Type = "dictionary", ValueType = "float", Minimum = 0, Maximum = 1 }, JsonValues.Parse("{\"a\":0.5}"), null, null);
dict.Entries.Add(new("b", "0.75"));
Check(dict.Build("weights")!["b"]!.GetValue<double>() == .75, "typed dictionary saves JSON object");
dict.Entries.Add(new("b", "0.1"));
Reject(() => dict.Build("weights"), "duplicate dictionary keys rejected");
Reject(() => new ConfigSchema { Sections = [new() { Properties = [renamed, Definition("oldCount", "bool")] }] }.Validate(), "schema aliases cannot collide with another field");
Reject(() => JsonValues.ParseObject("[]"), "non-object root rejected");
Check(JsonValues.ParseObject("{/*comment*/\"x\":1,}")["x"]!.GetValue<int>() == 1, "JSON comments and trailing commas accepted");

// Count pending settings independently of validation and of the number of input
// events. Replacing an object during Reset must retain the original baseline.
var counted = new ObjectDraft([renamed, nested], JsonValues.ParseObject("{\"count\":2,\"nested\":{\"flag\":false}}"),
    JsonValues.ParseObject("{\"count\":2,\"nested\":{\"flag\":true}}"));
var changes = new DraftChangeTracker(counted);
Check(changes.CountChanges() == 0, "loaded draft starts with zero pending changes");
counted.Fields[0].Text = "3";
Check(changes.CountChanges() == 1, "one changed setting counted");
counted.Fields[0].Text = "4";
Check(changes.CountChanges() == 1, "repeated edits to one setting count once");
counted.Fields[0].Text = "2";
Check(changes.CountChanges() == 0, "restoring original value clears pending change");
counted.Fields[1].Object!.Fields[0].BoolValue = true;
counted.Fields[0].Text = "3";
Check(changes.CountChanges() == 2, "nested leaves count separately from other settings");
counted.Fields[1].Reset();
Check(changes.CountChanges() == 2, "object reset does not replace tracking baseline");
counted.Fields[1].Object!.Fields[0].BoolValue = false;
counted.Fields[0].Text = "2";
Check(changes.CountChanges() == 0, "reverting after object reset returns to zero");
counted.Fields[0].Text = "unfinished-";
Check(changes.CountChanges() == 1 && counted.Fields[0].Error is null, "invalid pending input counted without triggering validation");
counted.Fields[0].Text = "2";
counted.Unknown.Add(new RawEntry("future", "{\"values\":[1,null]}"));
Check(changes.CountChanges() == 1, "unknown property addition counted");
counted.Unknown.Clear();
Check(changes.CountChanges() == 0, "undoing unknown addition clears change");
var unknownDraft = new ObjectDraft([], JsonValues.ParseObject("{\"future\":{\"x\":1}}"), new());
var unknownChanges = new DraftChangeTracker(unknownDraft);
unknownDraft.Unknown[0].Text = "{ \"x\" : 1 }";
Check(unknownChanges.CountChanges() == 0, "raw JSON formatting alone is not a value change");
unknownDraft.Unknown.Clear();
Check(unknownChanges.CountChanges() == 1, "unknown removal counted even when editor becomes hidden");
var listDraft = new ObjectDraft([new() { Property = "items", Type = "list" }], JsonValues.ParseObject("{\"items\":[]}"), new());
var listChanges = new DraftChangeTracker(listDraft);
listDraft.Fields[0].Entries.Add(new("", "a"));
listDraft.Fields[0].Entries.Add(new("", "b"));
Check(listChanges.CountChanges() == 1, "multiple list-row edits count as one changed setting");
listDraft.Fields[0].Entries.Clear();
Check(listChanges.CountChanges() == 0, "restoring list clears pending change");

// Unique scratch folder is the only directory these checks mutate.
var scratch = Path.Combine(Path.GetTempPath(), "dwv-config-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
try
{
    var schemaPath = Path.Combine(scratch, "configSchema.json");
    var defaultsPath = Path.Combine(scratch, "defaultConfig.jsonc");
    var configPath = Path.Combine(scratch, "config.jsonc");
    await File.WriteAllTextAsync(schemaPath, JsonSerializer.Serialize(schema));
    await File.WriteAllTextAsync(defaultsPath, "{\"count\":2,\"nested\":{\"flag\":true}}");
    var store = new ConfigFileStore(scratch);
    var session = await store.LoadAsync();
    Check(!File.Exists(configPath), "opening editor does not create a missing config");
    await store.SaveAsync(session);
    Check(File.Exists(configPath), "first Save creates config from defaults");
    var original = "{/*original config*/\"oldCount\":3,\"nested\":{\"flag\":true,\"unknown\":[false,null]},}";
    await File.WriteAllTextAsync(configPath, original);
    session = await store.LoadAsync();
    var stale = await store.LoadAsync();
    session.Draft.Fields[0].Text = "4";
    var save = await store.SaveAsync(session);
    Check(Directory.GetFiles(scratch, "*.backup.json").Length == 0, "saving creates no backup files");
    Check(JsonValues.ParseObject(await File.ReadAllTextAsync(configPath))["count"]!.GetValue<int>() == 4, "validated migration persisted");
    await RejectSave(async () => await store.SaveAsync(stale), "stale tab cannot overwrite newer save");
    for (var i = 0; i < 4; i++)
    {
        session = await store.LoadAsync();
        session.Draft.Fields[0].Text = i.ToString();
        await store.SaveAsync(session);
    }
    Check(Directory.GetFiles(scratch, "*.backup.json").Length == 0, "repeated saves create no backup files");
    session = await store.LoadAsync();
    var before = await File.ReadAllTextAsync(configPath);
    session.Draft.Fields[0].Text = "99";
    await RejectSave(async () => await store.SaveAsync(session), "invalid save rejected");
    Check(await File.ReadAllTextAsync(configPath) == before, "failed validation leaves disk unchanged");
    session = await store.LoadAsync();
    await File.AppendAllTextAsync(configPath, "\n//external edit");
    await RejectSave(async () => await store.SaveAsync(session), "external comment-only edit detected");
    session = await store.LoadAsync();
    await File.AppendAllTextAsync(schemaPath, " ");
    await RejectSave(async () => await store.SaveAsync(session), "changed schema detected");
    session = await store.LoadAsync();
    await File.AppendAllTextAsync(defaultsPath, " ");
    await RejectSave(async () => await store.SaveAsync(session), "changed defaults detected");
    session = await store.LoadAsync();
    File.SetAttributes(configPath, FileAttributes.ReadOnly);
    await RejectSave(async () => await store.SaveAsync(session), "read-only config write rejected");
    File.SetAttributes(configPath, FileAttributes.Normal);
    Check(Directory.GetFiles(scratch, "*.tmp").Length == 0, "failed write cleans temporary file");

    // Validate the actual mod schema/defaults as well as synthetic corner cases.
    if (args.Length > 0)
    {
        var modConfig = Path.GetFullPath(args[0]);
        File.Copy(Path.Combine(modConfig, "configSchema.json"), schemaPath, true);
        File.Copy(Path.Combine(modConfig, "defaultConfig.jsonc"), defaultsPath, true);
        File.Copy(defaultsPath, configPath, true);
        var actual = await store.LoadAsync();
        var actualResult = actual.Draft.Build();
        var expected = JsonValues.ParseObject(await File.ReadAllTextAsync(defaultsPath));
        Check(JsonNode.DeepEquals(expected, actualResult), "DWV defaults round-trip without changing any values");
        await store.SaveAsync(actual);
        Check(JsonNode.DeepEquals(expected, JsonValues.ParseObject(await File.ReadAllTextAsync(configPath))), "DWV schema saves successfully against a real config copy");
    }
}
finally
{
    // The path is constructed above from a fixed temp root plus a random name.
    // No user-supplied directory is ever recursively deleted.
    Directory.Delete(scratch, recursive: true);
}
Console.WriteLine($"All {passed} checks passed.");
