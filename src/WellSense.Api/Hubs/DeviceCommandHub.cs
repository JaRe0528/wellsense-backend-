using System.IdentityModel.Tokens.Jwt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WellSense.Application.Devices.IsDeviceOwnedByUser;

namespace WellSense.Api.Hubs;

/// <summary>
/// Android se conecta acá para recibir comandos en vivo — Web/Admin → API → SignalR
/// (este hub) → Android → Watch (ver 01-ARQUITECTURA-Y-STACK.md, flujo inverso de
/// comandos). A diferencia de DashboardHub (grupo por usuario), acá el grupo es por
/// DISPOSITIVO — un usuario puede tener varios, y un comando siempre va dirigido a uno
/// específico.
///
/// El JWT (mismo de siempre, del usuario) no lleva qué dispositivo es Android — mismo
/// gap ya resuelto en /sync (Bloque 4): el cliente debe decir explícitamente para qué
/// dispositivo se está conectando. Acá se resuelve con un método invocable por el
/// cliente (`RegisterForDevice`) en vez de un parámetro en la URL de conexión, para
/// poder verificar la propiedad del dispositivo (contra `Context.User`, no
/// `ICurrentUserService` — mismo motivo que DashboardHub, Bloque 5) antes de unir la
/// conexión al grupo.
/// </summary>
[Authorize]
public class DeviceCommandHub(ISender mediator) : Hub
{
    public async Task RegisterForDevice(Guid deviceId)
    {
        var userId = GetUserId();
        if (userId is null) return;

        var belongsToUser = await mediator.Send(new IsDeviceOwnedByUserQuery(userId.Value, deviceId));
        if (!belongsToUser) return; // silencioso — no se le confirma al cliente si un deviceId existe o no

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(deviceId));
    }

    private Guid? GetUserId()
    {
        var sub = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public static string GroupNameFor(Guid deviceId) => $"device-{deviceId}";
}
