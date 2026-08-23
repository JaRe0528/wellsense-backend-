using MediatR;

namespace WellSense.Application.Notifications.MarkNotificationRead;

public record MarkNotificationReadCommand(Guid CurrentUserId, Guid NotificationId) : IRequest<Unit>;
