namespace WellSense.Domain.SelfReports;

// P2 — modelada ya para no migrar en caliente, no se expone endpoint todavía
public class Experiment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public int DurationDays { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string BaselineMetric { get; set; } = "{}"; // jsonb
    public string ResultMetric { get; set; } = "{}"; // jsonb
    public DateTimeOffset CreatedAt { get; set; }
}
