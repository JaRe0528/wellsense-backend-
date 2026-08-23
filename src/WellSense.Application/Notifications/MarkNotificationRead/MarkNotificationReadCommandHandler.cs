using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Notifications.MarkNotificationRead;

public class MarkNotificationReadCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<MarkNotificationReadCommand, Unit>
{
    public async Task<Unit> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == request.CurrentUserId, ct);

        // Idempotente y silencioso, mismo criterio que Logout (Bloque 2): si no existe o
        // no es del usuario, o ya estaba leída, no hay nada de valor que proteger
        // devolviendo un error — simplemente no hace nada.
        if (notification is not null && notification.ReadAt is null)
        {
            notification.ReadAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Unit.Value;
    }
}
