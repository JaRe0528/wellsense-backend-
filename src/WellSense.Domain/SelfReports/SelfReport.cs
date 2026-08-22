namespace WellSense.Domain.SelfReports;

public class SelfReport
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public short Value { get; set; } // 1-5, "¿cómo te sientes?"
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
