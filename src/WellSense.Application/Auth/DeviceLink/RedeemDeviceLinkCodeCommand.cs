using MediatR;

namespace WellSense.Application.Auth.DeviceLink;

public record RedeemDeviceLinkCodeCommand(
    string Code,
    string? DeviceModel,
    string? OsVersion,
    string? AppVersion,
    string? IpAddress) : IRequest<RedeemDeviceLinkCodeResult>;

public record RedeemDeviceLinkCodeResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid UserId,
    string Email,
    Guid DeviceId);
