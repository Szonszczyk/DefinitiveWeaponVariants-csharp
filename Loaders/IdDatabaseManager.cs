using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using System.Reflection;

namespace DefinitiveWeaponVariants.Loaders
{
    internal class IdDatabaseManager
    {
        private readonly string modFolder;
        private readonly string folderPath;
        private readonly JsonUtil _jsonutil;
        private readonly ModHelper _modHelper;
        private readonly ISptLogger<DefinitiveWeaponVariants> _logger;

        public Dictionary<string, string> DbIds { get; private set; }
        private readonly Dictionary<string, string> _newIds = new(); // Only new IDs

        public IdDatabaseManager(
            ISptLogger<DefinitiveWeaponVariants> logger,
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
                _logger.LogWithColor($"[{GetType().Namespace}] Creating directory: {path}", LogTextColor.Green);
            }
        }

        private Dictionary<string, string> LoadAllIdFiles()
        {
            var combined = new Dictionary<string, string>();

            string[] files = Directory.GetFiles(folderPath, "*.json");

            if (files.Length == 0)
            {
                _logger.LogWithColor($"[{GetType().Namespace}] No ID files found in 99_Ids folder. Starting with empty database.", LogTextColor.Yellow);
                return combined;
            }

            foreach (var file in files)
            {
                var data = _modHelper.GetJsonDataFromFile<Dictionary<string, string>>(modFolder, file);

                if (data == null)
                {
                    _logger.LogWithColor($"[{GetType().Namespace}] Failed to load {file}", LogTextColor.Red);
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
            if (!DbIds.TryGetValue(sourceKey, out string? value))
            {
                // Create new ID, remember it and merge later
                value = new MongoId();
                DbIds[sourceKey] = value;
                _newIds[sourceKey] = value;
            }

            return value;
        }

        public void SaveDatabase()
        {
            if (_newIds.Count == 0)
                return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"ids_{timestamp}.json";
            string filePath = Path.Combine(folderPath, filename);

            string json = _jsonutil.Serialize(_newIds);
            File.WriteAllText(filePath, json);

            _logger.LogWithColor(
                $"[{GetType().Namespace}] New ID file created: {filePath}",
                LogTextColor.Yellow, LogBackgroundColor.Red);

            _logger.LogWithColor(
                $"[{GetType().Namespace}] IMPORTANT: This file MUST be saved and not deleted. If lost, user-created items will NOT load properly.",
                LogTextColor.Yellow, LogBackgroundColor.Red);

            _newIds.Clear();
        }
    }
}
