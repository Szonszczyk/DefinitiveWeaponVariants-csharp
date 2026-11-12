using Microsoft.AspNetCore.Http;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Servers.Http;

namespace DefinitiveWeaponVariants.Integrations;

// Guard the complete normal SPT request, including response serialization and
// scav generation in profile/raid-end paths. Delegate exactly once.
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Routers - 1)]
public sealed class APBSHttpListener(SptHttpListener normal, APBSIntegration integration) : IHttpListener
{
    public bool CanHandle(HttpContext context) => integration.CoordinateRequests && normal.CanHandle(context);

    public Task HandleAsync(MongoId sessionId, HttpContext context, CancellationToken cancellationToken = default)
    {
        var path = context.Request.Path.Value;
        bool trigger = path is "/client/game/bot/generate" or "/client/match/local/start"
            or "/client/game/profile/savage/regenerate" or "/client/game/profile/create"
            or "/client/match/local/end";
        return integration.HandleRequestAsync(() => normal.HandleAsync(sessionId, context, cancellationToken), trigger, cancellationToken);
    }
}
