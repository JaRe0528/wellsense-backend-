using MediatR;

namespace WellSense.Application.Devices.Commands.ListDeviceCommands;

public record ListDeviceCommandsQuery(Guid CurrentUserId, Guid DeviceId) : IRequest<IReadOnlyList<DeviceCommandResult>>;

public record DeviceCommandResult(
    Guid Id,
    string Type,
    string Payload,
    string Status,
    string? AckPayload,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset ExpiresAt);
