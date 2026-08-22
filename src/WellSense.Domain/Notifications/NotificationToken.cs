namespace WellSense.Domain.Notifications;

public class NotificationToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public string FcmToken { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
