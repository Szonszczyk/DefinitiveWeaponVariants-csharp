using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;

namespace DefinitiveWeaponVariants.Helpers;

/// <summary>Removes legacy files once, before the mod's database loaders are constructed.</summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload - 1)]
public sealed class FirstInstallHelper(CustomLogger logger, ModHelper modHelper) : IOnLoad
{
    private static readonly string[] LegacyFiles =
    [
        "db/01_Variants/20_BackportVariants.json",
        "db/01_Variants/21_BackportUniqueVariants.json",
        "db/02_Items/20_BackportItems.json",
        "db/02_Items/21_BackportUniqueItems.json",
        "db/03_Shortnames/BackportShortNames.json",
        "db/04_Presets/20_BackportPresets.json",
        "db/04_Presets/21_BackportUniquePresets.json",
        "db/99_Ids/20_BackportIds.json",
        "config/defaultConfig.jsonc"
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var markerPath = Path.Combine(modFolder, "config", ".first-install-complete");
        if (File.Exists(markerPath)) return Task.CompletedTask;

        try
        {
            foreach (var relativePath in LegacyFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.Combine(modFolder, relativePath);
                if (File.Exists(path)) File.Delete(path);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
            File.WriteAllText(markerPath, string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Error($"First installation cleanup failed: {exception.Message}. Resolve the file access problem and restart the server to retry.");
            throw;
        }

        logger.Important("First installation cleanup complete. Please configure the mod again at SIC: /definitive-weapon-variants/config");
        return Task.CompletedTask;
    }
}
