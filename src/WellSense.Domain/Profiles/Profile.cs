namespace WellSense.Domain.Profiles;

public class Profile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? BirthDate { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public string? Occupation { get; set; }
    public string? AvatarUrl { get; set; }
    /// <summary>
    /// IANA tz database name (ej. "America/Mexico_City"). Default 'UTC' a nivel de BD
    /// para perfiles que aún no la configuraron explícitamente — ver decisión de zona
    /// horaria en el HANDOFF de Bloque 3. Se valida como IANA id válido en
    /// UpsertMyProfileCommandValidator antes de persistir.
    /// </summary>
    public string Timezone { get; set; } = "UTC";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
