namespace WellSense.Domain.Wellness;

public class MlPrediction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ModelVersion { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Input { get; set; } = default!; // jsonb
    public string Output { get; set; } = default!; // jsonb
    public DateTimeOffset CreatedAt { get; set; }
}
