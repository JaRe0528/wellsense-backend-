using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Devices.UpdateDeviceHeartbeat;

public class UpdateDeviceHeartbeatCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<UpdateDeviceHeartbeatCommand, Unit>
{
    public async Task<Unit> Handle(UpdateDeviceHeartbeatCommand request, CancellationToken ct)
    {
        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.UserId == request.CurrentUserId, ct)
            ?? throw SyncDomainException.DeviceNotFound();

        if (request.Model is not null) device.Model = request.Model;
        if (request.OsVersion is not null) device.OsVersion = request.OsVersion;
        if (request.AppVersion is not null) device.AppVersion = request.AppVersion;
        device.LastSeenAt = clock.UtcNow;
        device.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
