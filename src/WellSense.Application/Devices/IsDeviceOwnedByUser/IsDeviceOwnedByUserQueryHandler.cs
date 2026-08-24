using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Devices.IsDeviceOwnedByUser;

public class IsDeviceOwnedByUserQueryHandler(IWellSenseDbContext db) : IRequestHandler<IsDeviceOwnedByUserQuery, bool>
{
    public Task<bool> Handle(IsDeviceOwnedByUserQuery request, CancellationToken ct)
        => db.Devices.AnyAsync(d => d.Id == request.DeviceId && d.UserId == request.UserId, ct);
}
