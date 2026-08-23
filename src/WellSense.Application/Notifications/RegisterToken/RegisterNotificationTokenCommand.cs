using MediatR;

namespace WellSense.Application.Notifications.RegisterToken;

public record RegisterNotificationTokenCommand(Guid CurrentUserId, Guid DeviceId, string FcmToken) : IRequest<Unit>;
