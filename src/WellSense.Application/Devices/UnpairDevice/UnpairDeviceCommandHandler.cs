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

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
