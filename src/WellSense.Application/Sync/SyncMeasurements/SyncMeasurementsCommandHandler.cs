using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Notifications.Events;
using WellSense.Domain.Devices;
using WellSense.Domain.Measurements;

namespace WellSense.Application.Sync.SyncMeasurements;

/// <summary>
/// Idempotencia en dos niveles, tal como se discutió:
///
/// 1) A nivel de BATCH: `request_id` (el "Idempotency-Key" que manda el cliente por
///    cada llamada a /sync) + `device_id` identifican de forma única una `sync_operations`
///    row (índice único `ux_sync_operations_device_request`). Si el cliente reintenta la
///    MISMA llamada (timeout de red, la app se cerró a medio subir la respuesta, etc.),
///    esta llamada encuentra la fila ya creada y devuelve el MISMO resultado sin volver
///    a tocar `measurements` — nunca se re-procesa un batch ya completado.
///
/// 2) A nivel de EVENTO individual: cada medición trae su propio `Id` (el eventId que
///    genera el sensor/watch al capturar la lectura). El índice único
///    `ux_measurements_device_event` (device_id, id, recorded_at) permite que el MISMO
///    evento aparezca en dos batches distintos (ej. un batch se recibió parcialmente y
///    el reintento del cliente incluye algunas mediciones que ya se habían guardado) sin
///    que eso cuente como error — simplemente se cuenta como "duplicada" y no se
///    reinserta.
///
/// Todo el trabajo (verificar duplicados, clasificar, insertar) viaja en una sola
/// `SaveChangesAsync` (una transacción implícita) — si algo falla a medio camino, nada
/// se compromete, así que no hace falta una máquina de estados PROCESSING→FAILED: o
/// el batch completo se confirma como COMPLETED, o no se confirma nada en absoluto.
///
/// Modificado en Bloque 5 (SignalR+FCM): se agregó `IPublisher publisher` para avisarle
/// al dashboard en vivo cuando este sync trajo datos nuevos de verdad (ver el final de
/// `Handle`). La lógica de idempotencia/clasificación de este handler NO se tocó —
/// solo se agregó la publicación del evento en el único punto de salida donde se
/// confirmaron datos genuinamente nuevos.
/// </summary>
public class SyncMeasurementsCommandHandler(
    IWellSenseDbContext db,
    IUniqueConstraintViolationDetector violationDetector,
    IDateTimeProvider clock,
    IPublisher publisher) : IRequestHandler<SyncMeasurementsCommand, SyncMeasurementsResult>
{
    private static readonly TimeSpan MaxFutureClockSkew = TimeSpan.FromMinutes(5);

    public async Task<SyncMeasurementsResult> Handle(SyncMeasurementsCommand request, CancellationToken ct)
    {
        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.UserId == request.CurrentUserId, ct)
            ?? throw SyncDomainException.DeviceNotFound();

        if (device.Status == DeviceStatus.Unpaired)
            throw SyncDomainException.DeviceNotFound(); // mismo error genérico — un dispositivo desvinculado no debe poder sincronizar

        // Replay idempotente: si este (device_id, request_id) ya se procesó, se devuelve
        // el mismo resultado sin volver a tocar measurements. Las razones de rechazo por
        // ítem no se persisten (solo los conteos) — un replay no las repite, ver HANDOFF.
        var existingOperation = await db.SyncOperations
            .FirstOrDefaultAsync(s => s.DeviceId == request.DeviceId && s.RequestId == request.RequestId, ct);
        if (existingOperation is not null)
            return MapToResult(existingOperation, []);

        var incomingIds = request.Measurements.Select(m => m.Id).Distinct().ToList();
        var existingPairs = await db.Measurements
            .Where(m => m.DeviceId == request.DeviceId && incomingIds.Contains(m.Id))
            .Select(m => new { m.Id, m.RecordedAt })
            .ToListAsync(ct);
        var existingKeys = existingPairs.Select(p => (p.Id, p.RecordedAt)).ToHashSet();

        var toInsert = new List<Measurement>();
        var rejected = new List<RejectedItem>();
        var duplicatedCount = 0;
        var now = clock.UtcNow;

        foreach (var item in request.Measurements)
        {
            if (existingKeys.Contains((item.Id, item.RecordedAt)))
            {
                duplicatedCount++;
                continue;
            }

            if (!MeasurementTypeExtensions.TryParseWireString(item.Type, out var type))
            {
                rejected.Add(new RejectedItem(item.Id, "INVALID_TYPE"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Unit))
            {
                rejected.Add(new RejectedItem(item.Id, "MISSING_UNIT"));
                continue;
            }

            if (item.RecordedAt > now.Add(MaxFutureClockSkew))
            {
                rejected.Add(new RejectedItem(item.Id, "RECORDED_AT_IN_FUTURE"));
                continue;
            }

            toInsert.Add(new Measurement
            {
                Id = item.Id,
                UserId = request.CurrentUserId,
                DeviceId = request.DeviceId,
                Type = type,
                Value = item.Value,
                Unit = item.Unit,
                RecordedAt = item.RecordedAt,
                SyncedAt = now,
                CreatedAt = now
            });
        }

        var operation = new SyncOperation
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            RequestId = request.RequestId,
            Status = SyncStatus.Completed,
            AcceptedCount = toInsert.Count,
            DuplicatedCount = duplicatedCount,
            RejectedCount = rejected.Count,
            CreatedAt = now
        };
        db.SyncOperations.Add(operation);
        db.Measurements.AddRange(toInsert);

        device.LastSeenAt = now;
        device.UpdatedAt = now;
        if (device.Status == DeviceStatus.Inactive) device.Status = DeviceStatus.Active;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (violationDetector.IsUniqueViolation(ex, "ux_sync_operations_device_request"))
        {
            // Carrera genuina: otra request concurrente con el mismo (device_id,
            // request_id) ganó y ya confirmó — se descarta este intento y se devuelve
            // el resultado del que sí se guardó, en vez de propagar el error.
            db.Entry(operation).State = EntityState.Detached;
            foreach (var m in toInsert) db.Entry(m).State = EntityState.Detached;

            var raced = await db.SyncOperations
                .FirstOrDefaultAsync(s => s.DeviceId == request.DeviceId && s.RequestId == request.RequestId, ct);
            if (raced is null) throw; // no debería pasar (la violación de índice implica que SÍ se guardó), pero si pasa, es un estado que no sabemos explicar — mejor propagar el error original que inventar una respuesta.
            return MapToResult(raced, []);
        }

        await PublishDashboardEventIfNeeded(operation, request.CurrentUserId, now, ct);
        return MapToResult(operation, rejected);
    }

    /// <summary>
    /// Se llama SOLO en el camino feliz (nunca en el replay idempotente ni en la
    /// recuperación de carrera) y SOLO si de verdad se aceptó al menos una medición
    /// nueva — un sync que solo trajo duplicados/rechazados no es información nueva
    /// para el dashboard, y el replay/carrera ya habrían publicado (o no) este mismo
    /// evento la primera vez que esos datos se procesaron de verdad.
    /// </summary>
    private async Task PublishDashboardEventIfNeeded(SyncOperation operation, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        if (operation.AcceptedCount > 0)
            await publisher.Publish(new MeasurementsSyncedEvent(userId, operation.AcceptedCount, now), ct);
    }

    private static SyncMeasurementsResult MapToResult(SyncOperation op, IReadOnlyList<RejectedItem> rejectedItems)
        => new(op.RequestId, op.Status.ToWireString(), op.AcceptedCount, op.DuplicatedCount, op.RejectedCount, rejectedItems);
}
