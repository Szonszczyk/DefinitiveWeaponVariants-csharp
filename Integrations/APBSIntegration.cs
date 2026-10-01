using DefinitiveWeaponVariants.Helpers;
using DefinitiveWeaponVariants.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using System.Reflection;


namespace DefinitiveWeaponVariants.Integrations;

[Injectable(InjectionType.Singleton)]
public class APBSIntegration(
    CustomLogger logger,
    ConfigData config,
    ModHelper modHelper,
    JsonUtil jsonUtil,
    ModDataStorage modDataStorage,
    ModCheck modCheck,
    TemplateTable templateTable
)
{
    private bool blacklistChanged = false;

    public void CheckModInstall()
    {
        if (config.EnableAPBSBlacklistGeneration && !modCheck.CheckInstalledMod("com.acidphantasm.progressivebotsystem", config.APBSFolderName))
        {
            logger.Warning($"Config option \"EnableAPBSBlacklistGeneration\" was enabled but APBS mod is missing (or APBS folder was not found)");
            config.EnableAPBSBlacklistGeneration = false;
        }
    }
    public void RunIntegration()
    {
        if (!config.EnableAPBSBlacklistGeneration) return;

        var modFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        string? parentDirectory = Directory.GetParent(modFolder)?.FullName;
        if (parentDirectory is null)
        {
            logger.Error($"Something went wrong in going back one level from {modFolder}");
            return;
        }
        var filePath = Path.Combine(parentDirectory, config.APBSFolderName, "blacklists.json");

        var rawBlacklist = modHelper.GetJsonDataFromFile<Dictionary<string, Dictionary<string, HashSet<string>>>>(modFolder, filePath);

        if (rawBlacklist is null)
        {
            logger.Error($"APBS blacklist not found in defauld directory: {filePath}");
            return;
        }

        rawBlacklist.TryGetValue("weaponBlacklist", out var weaponBlacklist);
        if (weaponBlacklist is null)
        {
            logger.Error($"APBS blacklist is incorrect, missing weaponBlacklist property");
            return;
        }

        var maxAPBSTier = 7;
        
        for (int i = 1; i <= maxAPBSTier; i++)
        {
            if (!weaponBlacklist.TryGetValue($"tier{i}Blacklist", out var tierBlacklist) || tierBlacklist is null)
            {
                logger.Error($"weaponBlacklist/tier{i}Blacklist is missing");
                continue;
            }

            var weapInTier = new HashSet<string>();
            foreach (var (q, weaps) in modDataStorage.VariantIdsByQuality)
            {
                var qualityBlacklisted = !config.APBSTierConfig.TryGetValue(q, out var tier) || tier == 0 || i < tier;
                foreach (var w in weaps)
                {
                    if (qualityBlacklisted)
                        weapInTier.Add(w);
                    else
                    {
                        if (config.APBSBlacklistedVariantTypes.Contains(modDataStorage.VariantTypes[w]))
                            weapInTier.Add(w);
                    }
                }
            }
            foreach (var weap in tierBlacklist)
            {
                if (weapInTier.Contains(weap) || modDataStorage.AllVariantIds.Contains(weap)) continue;
                if (templateTable.Items.ContainsKey(weap))
                {
                    weapInTier.Add(weap);
                }
                else
                {
                    logger.Warning($"Weapon {weap} in weaponBlacklist/tier{i}Blacklist is incorrect - removing");
                }
            }
            if (!weapInTier.SetEquals(tierBlacklist))
            {
                blacklistChanged = true;
                weaponBlacklist[$"tier{i}Blacklist"] = weapInTier;
            }
        }
        if (blacklistChanged)
        {
            string json = jsonUtil.Serialize(rawBlacklist, true);
            File.WriteAllText(filePath, json);
            logger.Info($"APBS blacklist has been updated");
            //logger.Error($"APBS blacklist has been updated. Please reload config in APBS web app or restart server!");
        }
        else
        {
            logger.Ok($"APBS blacklist is up to date");
        }
    }
    public async void RefreshBlacklist(CancellationToken cancellationToken)
    {
        if (!config.EnableAPBSBlacklistGeneration || !blacklistChanged) return;

        string reloadResult = await ReloadIfInstalledAsync(cancellationToken);

        switch (reloadResult)
        {
            case "Success":
                logger.Ok("Successfully reloaded the APBS blacklist");
                break;

            case "ActiveProcess":
                logger.Warning("APBS was already saving or reloading its configuration");
                break;

            default:
                logger.Warning($"Could not reload APBS configuration: {reloadResult}");
                break;
        }
    }

    private const string ModConfigType = "ProgressiveBotSystem.Globals.ModConfig";

    public async Task<string> ReloadIfInstalledAsync(
       CancellationToken cancellationToken
    )
    {
        Type? modConfigType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly =>
                assembly.GetType(ModConfigType, throwOnError: false))
            .FirstOrDefault(type => type is not null);

        if (modConfigType is null)
        {
            return "IntegrationUnavailable";
        }

        MethodInfo? reloadMethod = modConfigType.GetMethod(
            "ReloadConfig",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CancellationToken)],
            modifiers: null
        );

        if (reloadMethod is null)
        {
            return "IntegrationUnavailable";
        }

        try
        {
            object? invocation = reloadMethod.Invoke(
                null,
                [cancellationToken]
            );

            if (invocation is not Task task)
            {
                return "IntegrationUnavailable";
            }

            await task.ConfigureAwait(false);

            // ReloadConfig returns Task<ConfigOperationResult>.
            object? result = task
                .GetType()
                .GetProperty("Result")
                ?.GetValue(task);

            return result?.ToString() ?? "Unknown";
        }
        catch (TargetInvocationException ex)
        {
            // The useful exception is normally wrapped here.
            throw ex.InnerException ?? ex;
        }
    }
}
