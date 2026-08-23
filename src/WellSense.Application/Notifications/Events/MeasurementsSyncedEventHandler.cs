using MediatR;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Notifications.Events;

public class MeasurementsSyncedEventHandler(IDashboardNotifier notifier) : INotificationHandler<MeasurementsSyncedEvent>
{
    public Task Handle(MeasurementsSyncedEvent notification, CancellationToken ct)
    {
        // Nunca se dispara si acceptedCount es 0 (un sync que solo trajo duplicados/
        // rechazados no es información nueva para el dashboard) — ver dónde se publica
        // este evento en SyncMeasurementsCommandHandler.
        return notifier.NotifyUserAsync(
            notification.UserId,
            "measurements_synced",
            new { acceptedCount = notification.AcceptedCount, syncedAt = notification.SyncedAt },
            ct);
    }
}
