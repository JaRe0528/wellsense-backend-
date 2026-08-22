namespace WellSense.Domain.Measurements;

public enum MeasurementType { HeartRate, Steps, Spo2, Calories, SkinTemp }

public class Measurement
{
    public Guid Id { get; set; } // EventId del wearable, clave de idempotencia
    public Guid UserId { get; set; }
    public Guid DeviceId { get; set; }
    public MeasurementType Type { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = default!;
    public DateTimeOffset RecordedAt { get; set; } // partition key
    public DateTimeOffset? SyncedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
