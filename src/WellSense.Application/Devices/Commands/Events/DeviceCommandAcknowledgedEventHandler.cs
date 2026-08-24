using MediatR;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Devices.Commands.Events;

public class DeviceCommandAcknowledgedEventHandler(IDashboardNotifier notifier) : INotificationHandler<DeviceCommandAcknowledgedEvent>
{
    public Task Handle(DeviceCommandAcknowledgedEvent notification, CancellationToken ct)
        => notifier.NotifyUserAsync(
            notification.UserId,
            "device_command_acknowledged",
            new { deviceId = notification.DeviceId, commandId = notification.CommandId, status = notification.Status },
            ct);
}
