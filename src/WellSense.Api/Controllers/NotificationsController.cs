using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Notifications.ListMyNotifications;
using WellSense.Application.Notifications.MarkNotificationRead;
using WellSense.Application.Notifications.RegisterToken;
using WellSense.Application.Notifications.SendNotification;

namespace WellSense.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Registra/reemplaza el token FCM activo de un dispositivo propio.</summary>
    [HttpPost("tokens")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterToken(RegisterNotificationTokenRequest request, CancellationToken ct)
    {
        await mediator.Send(new RegisterNotificationTokenCommand(currentUser.UserId!.Value, request.DeviceId, request.FcmToken), ct);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> List([FromQuery] bool unreadOnly, CancellationToken ct)
    {
        var notifications = await mediator.Send(new ListMyNotificationsQuery(currentUser.UserId!.Value, unreadOnly), ct);
        return Ok(notifications.Select(n => new NotificationResponse(n.Id, n.Type, n.Title, n.Body, n.ReadAt, n.CreatedAt)));
    }

    /// <summary>Idempotente — marcar como leída una ya leída, o una que no existe, no da error.</summary>
    [HttpPut("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct)
    {
        await mediator.Send(new MarkNotificationReadCommand(currentUser.UserId!.Value, notificationId), ct);
        return NoContent();
    }

    /// <summary>
    /// Envía una notificación de prueba a sí mismo (crea el registro in-app y empuja a
    /// todos los tokens FCM registrados del usuario) — para que Web/Android puedan
    /// validar el flujo completo sin depender de que otro bloque ya dispare
    /// notificaciones reales.
    /// </summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(SendNotificationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SendNotificationResponse>> SendTest(SendTestNotificationRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SendNotificationCommand(
            currentUser.UserId!.Value, "TEST", request.Title, request.Body), ct);
        return Ok(new SendNotificationResponse(result.NotificationId, result.PushedCount, result.FailedPushCount));
    }
}
