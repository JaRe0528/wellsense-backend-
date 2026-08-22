namespace WellSense.Domain.Wellness;

public enum StressLevel { Low, Medium, High }

public class StressScore
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Score { get; set; }
    public StressLevel Level { get; set; }
    public decimal Confidence { get; set; }
    public string Factors { get; set; } = "{}"; // jsonb
    public DateTimeOffset CreatedAt { get; set; }
}
