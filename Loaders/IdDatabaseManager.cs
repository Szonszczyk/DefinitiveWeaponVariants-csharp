using DefinitiveWeaponVariants.Helpers;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils;
using System.Reflection;

namespace DefinitiveWeaponVariants.Loaders;

[Injectable(InjectionType.Singleton)]
public class IdDatabaseManager
{
    private readonly string modFolder;
    private readonly string folderPath;
    private readonly JsonUtil _jsonutil;
    private readonly ModHelper _modHelper;
    private readonly CustomLogger _logger;

    public Dictionary<string, string> DbIds { get; private set; }
    private readonly Dictionary<string, string> _newIds = new(); // Only new IDs

    // Debug switch
    private const bool DebugIdUsage = false;

    // Track which IDs were actually requested via GetCustomId
    private readonly HashSet<string> _usedKeys = new();

    public IdDatabaseManager(
        CustomLogger logger,
        ModHelper modHelper,
        JsonUtil jsonUtil)
    {
        _jsonutil = jsonUtil;
        _logger = logger;
        _modHelper = modHelper;

        modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        folderPath = Path.Combine(modFolder, "db", "99_Ids");

        EnsureFolderExists(folderPath);

        DbIds = LoadAllIdFiles();
    }

    private void EnsureFolderExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.Ok($"Creating directory: {path}");
        }
    }

    private Dictionary<string, string> LoadAllIdFiles()
    {
        var combined = new Dictionary<string, string>();

        string[] files = Directory.GetFiles(folderPath, "*.json");

        if (files.Length == 0)
        {
            _logger.Warning($"No ID files found in 99_Ids folder. Starting with empty database.");
            return combined;
        }

        foreach (var file in files)
        {
            var data = _modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modFolder, file);

            if (data == null)
            {
                _logger.Error($"Failed to load {file}");
                continue;
            }

            foreach (var entry in data)
            {
                // Avoid duplicate keys by ignoring later ones
                if (!combined.ContainsKey(entry.Key))
                    combined.Add(entry.Key, entry.Value);
            }
        }

        return combined;
    }

    public string GetCustomId(string sourceKey)
    {
        // Mark as used (even if it already existed)
        _usedKeys.Add(sourceKey);

        if (!DbIds.TryGetValue(sourceKey, out string? value))
        {
            value = new MongoId();
            DbIds[sourceKey] = value;
            _newIds[sourceKey] = value;
        }

        return value;
    }

    public void SaveDatabase()
    {
        LogIdUsageStats();
        SaveUsedIdsDatabase();

        if (_newIds.Count == 0)
            return;

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filename = $"ids_{timestamp}.json";
        string filePath = Path.Combine(folderPath, filename);

        string json = _jsonutil.Serialize(_newIds);
        File.WriteAllText(filePath, json);

        _logger.Important($"New ID file created: {filePath}");

        _logger.Important($"IMPORTANT: This file MUST be saved and not deleted. If lost, user-created items will NOT load properly.");

        _newIds.Clear();
    }

    private void LogIdUsageStats()
    {
        if (!DebugIdUsage)  return;

        int total = DbIds.Count;
        int used = _usedKeys.Count;
        int unused = total - used;

        _logger.Info($"ID usage summary → Total: {total}, Used: {used}, Unused: {unused}");

        if (unused > 0)
        {
            _logger.Info($"WARNING: {unused} IDs are never used");
        }
    }
    private void SaveUsedIdsDatabase()
    {
        if (!DebugIdUsage) return;

        var usedOnly = DbIds
            .Where(kvp => _usedKeys.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (usedOnly.Count == 0) return;

        string filePath = Path.Combine(folderPath, "_ids_used_combined.json");
        string json = _jsonutil.Serialize(usedOnly);

        File.WriteAllText(filePath, json);

        _logger.Info($"Saved combined USED ID database: {filePath}");
    }
}
