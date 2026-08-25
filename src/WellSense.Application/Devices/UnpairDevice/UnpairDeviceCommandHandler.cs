using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Devices;

namespace WellSense.Application.Devices.UnpairDevice;

/// <summary>
/// Soft-unpair (status='UNPAIRED'), nunca DELETE físico — measurements, sync_operations y
/// notification_tokens tienen FKs NOT NULL hacia devices(id); borrar la fila destruiría
/// el historial de mediciones de ese dispositivo.
///
/// Modificado en Bloque 10 (auditoría completa): se agregó un registro en `audit_logs`
/// (`device_unpaired`) — no se registraba antes.
/// </summary>
public class UnpairDeviceCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<UnpairDeviceCommand, Unit>
{
    public async Task<Unit> Handle(UnpairDeviceCommand request, CancellationToken ct)
    {
        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.UserId == request.CurrentUserId, ct)
            ?? throw SyncDomainException.DeviceNotFound();

        device.Status = DeviceStatus.Unpaired;
        device.UpdatedAt = clock.UtcNow;

        db.AuditLogs.Add(new WellSense.Domain.Identity.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.CurrentUserId,
            Action = "device_unpaired",
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { deviceId = device.Id }),
            CreatedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
