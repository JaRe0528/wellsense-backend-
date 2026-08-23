using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WellSense.Api.Hubs;

/// <summary>
/// Un solo propósito: cada conexión se une automáticamente a un grupo scoped a su propio
/// usuario (`user-{userId}`), y el servidor empuja eventos a ese grupo — nunca al revés.
/// No expone métodos invocables por el cliente porque no hace falta: el dashboard web
/// solo escucha, nunca le pide nada al hub directamente (todo lo que necesita "pedir"
/// sigue siendo REST normal).
///
/// Deliberadamente lee el usuario de `Context.User` (el ClaimsPrincipal propio del Hub),
/// NO de `ICurrentUserService`/`IHttpContextAccessor` como el resto de la Api — ese es el
/// patrón recomendado específicamente para SignalR: tras el upgrade a WebSocket, el
/// `HttpContext` de la request HTTP original no es una fuente confiable para toda la vida
/// de la conexión, mientras que `Context.User` sí lo es (SignalR lo repuebla a partir del
/// mismo pipeline de autenticación en cada conexión).
/// </summary>
[Authorize]
public class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(userId.Value));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId is not null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNameFor(userId.Value));

        await base.OnDisconnectedAsync(exception);
    }

    private Guid? GetUserId()
    {
        var sub = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public static string GroupNameFor(Guid userId) => $"user-{userId}";
}
