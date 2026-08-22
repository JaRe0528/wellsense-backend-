namespace WellSense.Domain.Measurements;

public class SleepSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int DurationMinutes { get; set; } // columna generada en BD, solo lectura
    public string Stages { get; set; } = "{}"; // jsonb
    public DateTimeOffset CreatedAt { get; set; }
}
