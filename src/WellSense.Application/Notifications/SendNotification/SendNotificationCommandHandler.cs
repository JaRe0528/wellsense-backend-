using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Notifications;

namespace WellSense.Application.Notifications.SendNotification;

/// <summary>
/// El registro en `notifications` (el centro de notificaciones in-app) SIEMPRE se crea,
/// exista o no exista ningún token FCM registrado, y aunque el push falle contra todos
/// los tokens — el push es un mejor-esfuerzo de entrega inmediata, no la fuente de
/// verdad de si la notificación "existe" para el usuario. Un usuario que abre la app más
/// tarde debe poder ver la notificación en su historial aunque el push nunca haya
/// llegado (token expirado, sin conexión en ese momento, etc.).
/// </summary>
public class SendNotificationCommandHandler(
    IWellSenseDbContext db,
    IPushNotificationSender pushSender,
    IDateTimeProvider clock) : IRequestHandler<SendNotificationCommand, SendNotificationResult>
{
    public async Task<SendNotificationResult> Handle(SendNotificationCommand request, CancellationToken ct)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Type = request.Type,
            Title = request.Title,
            Body = request.Body,
            CreatedAt = clock.UtcNow
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        var tokens = await db.NotificationTokens
            .Where(t => t.UserId == request.UserId)
            .Select(t => t.FcmToken)
            .ToListAsync(ct);

        var pushed = 0;
        var failed = 0;
        foreach (var token in tokens)
        {
            var ok = await pushSender.TrySendAsync(token, request.Title, request.Body, ct);
            if (ok) pushed++; else failed++;
        }

        return new SendNotificationResult(notification.Id, pushed, failed);
    }
}
