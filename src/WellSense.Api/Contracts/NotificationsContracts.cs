namespace WellSense.Api.Contracts;

public record RegisterNotificationTokenRequest(Guid DeviceId, string FcmToken);

public record NotificationResponse(Guid Id, string Type, string Title, string Body, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);

public record SendTestNotificationRequest(string Title, string Body);
public record SendNotificationResponse(Guid NotificationId, int PushedCount, int FailedPushCount);
