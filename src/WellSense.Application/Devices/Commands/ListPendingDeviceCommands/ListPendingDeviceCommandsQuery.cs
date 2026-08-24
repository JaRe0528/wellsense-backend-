using MediatR;
using WellSense.Application.Devices.Commands.ListDeviceCommands;

namespace WellSense.Application.Devices.Commands.ListPendingDeviceCommands;

public record ListPendingDeviceCommandsQuery(Guid CurrentUserId, Guid DeviceId) : IRequest<IReadOnlyList<DeviceCommandResult>>;
