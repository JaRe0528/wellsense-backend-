namespace WellSense.Domain.Devices;

public enum DeviceCommandType { StartMonitoring, StopMonitoring, ChangeInterval, SyncNow, RequestStatus }
public enum DeviceCommandStatus { Pending, Delivered, Acknowledged, Failed, Expired }

public class DeviceCommand
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid UserId { get; set; }
    public DeviceCommandType Type { get; set; }
    public string Payload { get; set; } = "{}"; // jsonb
    public DeviceCommandStatus Status { get; set; } = DeviceCommandStatus.Pending;
    public string? AckPayload { get; set; } // jsonb
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
