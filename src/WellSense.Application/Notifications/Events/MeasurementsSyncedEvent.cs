using MediatR;

namespace WellSense.Application.Notifications.Events;

/// <summary>
/// Evento de integración (no un comando/query): se publica DESPUÉS de que un sync ya se
/// confirmó en BD, para empujar la actualización al dashboard en vivo por SignalR. Vive
/// en su propio namespace ("Events", no "Notification" a secas) para no repetir el
/// choque de namespace de Bloque 2/3 (WellSense.Domain.Notifications.Notification ya
/// existe como entidad).
/// </summary>
public record MeasurementsSyncedEvent(Guid UserId, int AcceptedCount, DateTimeOffset SyncedAt) : INotification;
