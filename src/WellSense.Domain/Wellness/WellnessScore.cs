namespace WellSense.Domain.Wellness;

public class WellnessScore
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Score { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
