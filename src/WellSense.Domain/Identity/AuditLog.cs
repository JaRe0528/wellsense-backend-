namespace WellSense.Domain.Identity;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;
    public string Metadata { get; set; } = "{}"; // jsonb, mapeado como string crudo
    public string? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
