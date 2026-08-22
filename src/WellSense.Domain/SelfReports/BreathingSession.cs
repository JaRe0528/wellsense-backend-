namespace WellSense.Domain.SelfReports;

public class BreathingSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public decimal? HrBefore { get; set; }
    public decimal? HrAfter { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
