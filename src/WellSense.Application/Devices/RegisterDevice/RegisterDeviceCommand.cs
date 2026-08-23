using MediatR;

namespace WellSense.Application.Devices.RegisterDevice;

public record RegisterDeviceCommand(
    Guid CurrentUserId,
    string Type,
    string? Model,
    string? OsVersion,
    string? AppVersion) : IRequest<Guid>;
