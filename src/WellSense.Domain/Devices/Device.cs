namespace WellSense.Domain.Devices;

public enum DeviceType { Phone, Watch, Web }
public enum DeviceStatus { Active, Inactive, Unpaired }

public class Device
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DeviceType Type { get; set; }
    public string? Model { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Active;
    public DateTimeOffset PairedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
