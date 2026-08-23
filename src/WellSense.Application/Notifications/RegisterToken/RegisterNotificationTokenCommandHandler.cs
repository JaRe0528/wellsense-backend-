using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Notifications;

namespace WellSense.Application.Notifications.RegisterToken;

/// <summary>
/// Invariante que impone este handler (no la BD — el único índice único real es
/// device_id+fcm_token, no device_id solo): un dispositivo tiene A LO SUMO un token FCM
/// activo a la vez. Los tokens de FCM rotan con el tiempo (reinstalación de la app,
/// limpieza de caché, etc.) — si simplemente insertáramos uno nuevo cada vez sin borrar
/// los anteriores del mismo dispositivo, se acumularían tokens muertos a los que
/// seguiríamos intentando enviar push innecesariamente.
/// </summary>
public class RegisterNotificationTokenCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<RegisterNotificationTokenCommand, Unit>
{
    public async Task<Unit> Handle(RegisterNotificationTokenCommand request, CancellationToken ct)
    {
        var deviceExists = await db.Devices
            .AnyAsync(d => d.Id == request.DeviceId && d.UserId == request.CurrentUserId, ct);
        if (!deviceExists)
            throw SyncDomainException.DeviceNotFound();

        var previousTokens = await db.NotificationTokens
            .Where(t => t.DeviceId == request.DeviceId)
            .ToListAsync(ct);
        db.NotificationTokens.RemoveRange(previousTokens);

        db.NotificationTokens.Add(new NotificationToken
        {
            Id = Guid.NewGuid(),
            UserId = request.CurrentUserId,
            DeviceId = request.DeviceId,
            FcmToken = request.FcmToken,
            CreatedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
