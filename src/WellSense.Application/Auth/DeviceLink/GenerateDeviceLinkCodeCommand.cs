using MediatR;

namespace WellSense.Application.Auth.DeviceLink;

public record GenerateDeviceLinkCodeCommand(Guid CurrentUserId) : IRequest<GenerateDeviceLinkCodeResult>;

public record GenerateDeviceLinkCodeResult(string Code, DateTimeOffset ExpiresAt);
