using System.Reflection;
using DefinitiveWeaponVariants.Helpers;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Utils;

namespace DefinitiveWeaponVariants.Services;

/// <summary>
/// Publishes db/10_Slots images for the DWV client. The filename without its extension
/// must match the slot's Name (for example, mod_core.png). Restart the server and game
/// after changing images. PNG and JPEG are supported by Unity's image decoder.
/// Inspired by WTT-CommonLib's MIT-licensed slot image service (GrooveypenguinX).
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload + 54001)]
public sealed class SlotImageService(ModHelper modHelper, JsonUtil jsonUtil, CustomLogger logger) : IOnLoad
{
    public const string Endpoint = "/definitiveweaponvariants/slot-images";
    private string payload = "{}";

    // Cache the complete response at startup; requests never reread files or see a partial load.
    public string GetPayload() => payload;

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(
            modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly()), "db", "10_Slots");
        var images = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!Directory.Exists(directory))
        {
            payload = "{}";
            logger.Debug($"No slot image directory at '{directory}'.");
            return;
        }

        try
        {
            foreach (var path in Directory.GetFiles(directory).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var extension = Path.GetExtension(path);
                if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                    continue;

                var slotName = Path.GetFileNameWithoutExtension(path);
                if (images.ContainsKey(slotName))
                {
                    logger.Warning($"Duplicate slot image '{Path.GetFileName(path)}' ignored; '{slotName}' already has an image.");
                    continue;
                }

                try
                {
                    var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                    if (bytes.Length == 0)
                    {
                        logger.Warning($"Empty slot image '{Path.GetFileName(path)}' ignored.");
                        continue;
                    }

                    images.Add(slotName, Convert.ToBase64String(bytes));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    logger.Warning($"Could not read slot image '{Path.GetFileName(path)}': {exception.Message}");
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Warning($"Could not scan slot images at '{directory}': {exception.Message}");
        }

        payload = jsonUtil.Serialize(images)
            ?? throw new InvalidOperationException("Could not serialize DWV slot images.");
        logger.Debug($"Loaded {images.Count} custom slot image(s).");
    }
}

// Keep the SPT route beside its service so this feature stays in one source file.
[Injectable(TypePriority = OnLoadOrder.Routers)]
public sealed class SlotImageRouter(JsonUtil jsonUtil, SlotImageService images) : StaticRouter(jsonUtil,
[
    new RouteAction(SlotImageService.Endpoint,
        (_, _, _, _, _) => ValueTask.FromResult<object>(images.GetPayload()))
]);
