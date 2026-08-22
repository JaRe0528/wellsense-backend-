namespace WellSense.Domain.Measurements;

public enum SyncStatus { Processing, Completed, Failed }

public class SyncOperation
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string RequestId { get; set; } = default!; // Idempotency-Key del header HTTP
    public SyncStatus Status { get; set; } = SyncStatus.Processing;
    public int AcceptedCount { get; set; }
    public int DuplicatedCount { get; set; }
    public int RejectedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
