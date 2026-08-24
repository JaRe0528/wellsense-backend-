using Microsoft.AspNetCore.SignalR;
using WellSense.Api.Hubs;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Api.Services;

public class SignalRDeviceCommandNotifier(IHubContext<DeviceCommandHub> hubContext) : IDeviceCommandNotifier
{
    public Task NotifyDeviceAsync(Guid deviceId, object command, CancellationToken ct = default)
        => hubContext.Clients
            .Group(DeviceCommandHub.GroupNameFor(deviceId))
            .SendAsync("deviceCommand", command, ct);
}
