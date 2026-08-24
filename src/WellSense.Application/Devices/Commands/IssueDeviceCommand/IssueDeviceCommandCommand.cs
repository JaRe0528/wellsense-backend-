using MediatR;

namespace WellSense.Application.Devices.Commands.IssueDeviceCommand;

public record IssueDeviceCommandCommand(
    Guid CurrentUserId, Guid DeviceId, string Type, string? PayloadJson) : IRequest<IssueDeviceCommandResult>;

public record IssueDeviceCommandResult(Guid CommandId, string Type, string Status, DateTimeOffset CreatedAt);
