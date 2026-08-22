namespace WellSense.Domain.Devices;

public class DeviceLinkCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = default!; // HMAC-SHA256(code, pepper) — nunca el código en claro
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
