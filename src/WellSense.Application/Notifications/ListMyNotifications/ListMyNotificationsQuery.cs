using MediatR;

namespace WellSense.Application.Notifications.ListMyNotifications;

public record ListMyNotificationsQuery(Guid CurrentUserId, bool UnreadOnly) : IRequest<IReadOnlyList<NotificationResult>>;

public record NotificationResult(Guid Id, string Type, string Title, string Body, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);
