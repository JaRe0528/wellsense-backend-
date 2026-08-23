using MediatR;

namespace WellSense.Application.Devices.ListMyDevices;

public record ListMyDevicesQuery(Guid CurrentUserId) : IRequest<IReadOnlyList<DeviceResult>>;

public record DeviceResult(
    Guid Id,
    string Type,
    string? Model,
    string? OsVersion,
    string? AppVersion,
    string Status,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset PairedAt);
