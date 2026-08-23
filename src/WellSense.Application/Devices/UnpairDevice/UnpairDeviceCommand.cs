using MediatR;

namespace WellSense.Application.Devices.UnpairDevice;

public record UnpairDeviceCommand(Guid CurrentUserId, Guid DeviceId) : IRequest<Unit>;
