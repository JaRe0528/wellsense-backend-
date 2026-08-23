using MediatR;

namespace WellSense.Application.Devices.UpdateDeviceHeartbeat;

public record UpdateDeviceHeartbeatCommand(
    Guid CurrentUserId,
    Guid DeviceId,
    string? Model,
    string? OsVersion,
    string? AppVersion) : IRequest<Unit>;
