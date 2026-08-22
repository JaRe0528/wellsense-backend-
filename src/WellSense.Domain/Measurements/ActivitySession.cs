namespace WellSense.Domain.Measurements;

public class ActivitySession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = default!;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int? Steps { get; set; }
    public decimal? DistanceM { get; set; }
    public decimal? Calories { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
