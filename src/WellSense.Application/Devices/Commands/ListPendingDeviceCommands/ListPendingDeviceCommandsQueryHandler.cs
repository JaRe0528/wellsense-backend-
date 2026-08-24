using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Devices.Commands.ListDeviceCommands;
using WellSense.Domain.Devices;

namespace WellSense.Application.Devices.Commands.ListPendingDeviceCommands;

/// <summary>
/// Para que Android recupere lo que se haya perdido: PENDING (nunca se logró empujar) o
/// DELIVERED (se empujó pero nadie confirma haberlo recibido todavía) — nunca incluye
/// ACKNOWLEDGED/FAILED/EXPIRED. Pensado para llamarse al reconectar, mismo principio de
/// "el monitoreo nunca depende de Internet" del documento de arquitectura.
/// </summary>
public class ListPendingDeviceCommandsQueryHandler(IWellSenseDbContext db)
    : IRequestHandler<ListPendingDeviceCommandsQuery, IReadOnlyList<DeviceCommandResult>>
{
    public async Task<IReadOnlyList<DeviceCommandResult>> Handle(ListPendingDeviceCommandsQuery request, CancellationToken ct)
    {
        var deviceExists = await db.Devices.AnyAsync(d => d.Id == request.DeviceId && d.UserId == request.CurrentUserId, ct);
        if (!deviceExists)
            throw SyncDomainException.DeviceNotFound();

        var commands = await db.DeviceCommands
            .Where(c => c.DeviceId == request.DeviceId
                && (c.Status == DeviceCommandStatus.Pending || c.Status == DeviceCommandStatus.Delivered))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        return commands
            .Select(c => new DeviceCommandResult(
                c.Id, c.Type.ToWireString(), c.Payload, c.Status.ToString().ToUpperInvariant(), c.AckPayload,
                c.CreatedAt, c.DeliveredAt, c.AcknowledgedAt, c.ExpiresAt))
            .ToList();
    }
}
