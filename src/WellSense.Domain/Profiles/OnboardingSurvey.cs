namespace WellSense.Domain.Profiles;

public enum DeclaredStressLevel { MuyBajo, Bajo, Moderado, Alto, MuyAlto }

public class OnboardingSurvey
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string? UsualSchedule { get; set; }
    public string? SleepSchedule { get; set; }
    public string? DeclaredActivityLevel { get; set; }
    public DeclaredStressLevel DeclaredStressLevel { get; set; }
    public string? DeclaredSleepQuality { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
