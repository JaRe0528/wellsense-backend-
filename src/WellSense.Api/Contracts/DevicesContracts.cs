namespace WellSense.Api.Contracts;

public record DeviceResponse(
    Guid Id,
    string Type,
    string? Model,
    string? OsVersion,
    string? AppVersion,
    string Status,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset PairedAt);

public record RegisterDeviceRequest(string Type, string? Model, string? OsVersion, string? AppVersion);
public record RegisterDeviceResponse(Guid Id);

public record UpdateDeviceHeartbeatRequest(string? Model, string? OsVersion, string? AppVersion);
