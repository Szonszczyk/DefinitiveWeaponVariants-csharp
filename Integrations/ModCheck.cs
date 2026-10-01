using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Mod;
using System.Reflection;
using Path = System.IO.Path;

namespace DefinitiveWeaponVariants.Integrations;

[Injectable(InjectionType.Singleton)]
public class ModCheck(
    CustomLogger logger,
    ConfigData config,
    ModHelper modHelper,
    IReadOnlyList<SptMod> modlist
)
{
    private readonly ConfigData modConfig = config;

    public void CheckMods()
    {
        if (modConfig.AmonyaTraderMode)
        {
            if (!CheckInstalledMod("com.szonszczyk.amonya"))
            {
                logger.Warning($"Config option \"AmonyaTraderMode\" was enabled but Amonya mod is missing");
                modConfig.AmonyaTraderMode = false;
            }
            else
            {
                modConfig.SpecialAmmoBuyableEnabled = false;
            }
        }
    }
    public bool CheckInstalledMod(string guid, string? modfolder = null)
    {
        var mod = modlist.ToList().Find(t => t.ModMetadata.ModGuid == guid);
        if (mod is null) return false;
        if (modfolder == null) return true;
        var modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        string? parentDirectory = Directory.GetParent(modFolder)?.FullName;
        if (parentDirectory == null) return false;
        var filePath = Path.Combine(parentDirectory, modfolder);
        return Directory.Exists(filePath);
    }
}
