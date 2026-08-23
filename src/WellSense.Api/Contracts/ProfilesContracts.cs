namespace WellSense.Api.Contracts;

public record ProfileResponse(
    Guid Id,
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate,
    decimal? WeightKg,
    decimal? HeightCm,
    string? Occupation,
    string? AvatarUrl,
    string Timezone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record UpsertProfileRequest(
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate,
    decimal? WeightKg,
    decimal? HeightCm,
    string? Occupation,
    string? AvatarUrl,
    string Timezone);

public record GoalResponse(Guid Id, string Type, decimal TargetValue, DateTimeOffset CreatedAt);
public record AddGoalRequest(string Type, decimal TargetValue);
public record AddGoalResponse(Guid Id);

public record OnboardingSurveyResponse(
    string? UsualSchedule,
    string? SleepSchedule,
    string? DeclaredActivityLevel,
    string DeclaredStressLevel,
    string? DeclaredSleepQuality,
    DateTimeOffset CreatedAt);

public record UpsertOnboardingSurveyRequest(
    string? UsualSchedule,
    string? SleepSchedule,
    string? DeclaredActivityLevel,
    string DeclaredStressLevel,
    string? DeclaredSleepQuality);
