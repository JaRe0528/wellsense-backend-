using MediatR;

namespace WellSense.Application.Devices.Commands.AcknowledgeDeviceCommand;

public record AcknowledgeDeviceCommandCommand(
    Guid CurrentUserId, Guid DeviceId, Guid CommandId, string Status, string? AckPayloadJson) : IRequest<Unit>;
