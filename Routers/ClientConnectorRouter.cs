using DefinitiveWeaponVariants.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Utils;

namespace DefinitiveWeaponVariants.Routers;

[Injectable(TypePriority = OnLoadOrder.Routers)]
public class ClientConnectorRouter(
    JsonUtil jsonUtil,
    ClientConnectorService connector
) : StaticRouter(jsonUtil,
[
    new RouteAction(
        "/definitiveweaponvariants/variant-colors",
        (url, info, sessionId, output, cancellationToken) =>
            new ValueTask<object>(
                jsonUtil.Serialize(connector.PrepareRouterModelVariantColors())
                    ?? throw new InvalidOperationException("Could not serialize DWV variant colors")
            )
    ),
    new RouteAction(
        "/definitiveweaponvariants/item-colors",
        (url, info, sessionId, output, cancellationToken) =>
            new ValueTask<object>(
                jsonUtil.Serialize(connector.PrepareRouterModelItemColors())
                    ?? throw new InvalidOperationException("Could not serialize DWV item colors")
            )
    )
]);
