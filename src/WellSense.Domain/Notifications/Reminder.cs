namespace WellSense.Domain.Notifications;

public enum ReminderType { Manual, Auto }

public class Reminder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ReminderType Type { get; set; }
    public string Message { get; set; } = default!;
    public DateTimeOffset ScheduledAt { get; set; }
    public int CooldownMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
