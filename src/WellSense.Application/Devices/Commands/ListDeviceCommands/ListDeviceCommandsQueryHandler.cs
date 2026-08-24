using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Devices.Commands.ListDeviceCommands;

public class ListDeviceCommandsQueryHandler(IWellSenseDbContext db)
    : IRequestHandler<ListDeviceCommandsQuery, IReadOnlyList<DeviceCommandResult>>
{
    public async Task<IReadOnlyList<DeviceCommandResult>> Handle(ListDeviceCommandsQuery request, CancellationToken ct)
    {
        var deviceExists = await db.Devices.AnyAsync(d => d.Id == request.DeviceId && d.UserId == request.CurrentUserId, ct);
        if (!deviceExists)
            throw SyncDomainException.DeviceNotFound();

        // Se materializa primero, luego se traduce el enum a string en memoria — mismo
        // motivo de siempre (Type/Status usan HasConversion con lambdas propias).
        var commands = await db.DeviceCommands
            .Where(c => c.DeviceId == request.DeviceId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return commands
            .Select(c => new DeviceCommandResult(
                c.Id, c.Type.ToWireString(), c.Payload, c.Status.ToString().ToUpperInvariant(), c.AckPayload,
                c.CreatedAt, c.DeliveredAt, c.AcknowledgedAt, c.ExpiresAt))
            .ToList();
    }
}
