namespace WellSense.Domain.Profiles;

public class Goal
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Type { get; set; } = default!;
    public decimal TargetValue { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
