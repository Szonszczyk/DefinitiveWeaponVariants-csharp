using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SPTarkov.DI.Annotations;

namespace DefinitiveWeaponVariants.Integrations;

// Wrap the existing pipeline. Resolving SptHttpListener here would construct a
// second HttpRouter and repeat constructor-time registrations in other mods.
[Injectable(InjectionType.Singleton)]
public sealed class APBSStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(HandleAsync);
        next(app);
    };

    private static Task HandleAsync(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;

        // Startup filters run before UseWebSockets, so check the upgrade headers
        // as well as the feature. CONNECT covers HTTP/2 extended connections.
        // Blazor and SPT notifications also support long polling. These
        // transports do not consume APBS bot data and must not delay a refresh.
        if (context.WebSockets.IsWebSocketRequest
            || request.Headers.ContainsKey("Upgrade")
            || HttpMethods.IsConnect(request.Method)
            || request.Path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/notifierServer", StringComparison.OrdinalIgnoreCase)
            || request.Path.StartsWithSegments("/push/notifier/get", StringComparison.OrdinalIgnoreCase)
            || (request.Path == "/" && request.Query.ContainsKey("last_id"))
            || (HttpMethods.IsGet(request.Method)
                && request.Headers.Accept.Any(value =>
                    value?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)))
            return next(context);

        var integration = context.RequestServices.GetRequiredService<APBSIntegration>();
        bool trigger = request.Path.Value is "/client/game/bot/generate" or "/client/match/local/start"
            or "/client/game/profile/savage/regenerate" or "/client/game/profile/create"
            or "/client/match/local/end";

        // Keep the lease until downstream handling and response serialization
        // finish. HandleRequestAsync passes through when coordination is inactive.
        return integration.HandleRequestAsync(() => next(context), trigger, context.RequestAborted);
    }
}
