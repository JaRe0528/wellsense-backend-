namespace WellSense.Api.Contracts;

public record SleepSessionResponse(Guid Id, DateTimeOffset StartAt, DateTimeOffset EndAt, int DurationMinutes, string Stages, DateTimeOffset CreatedAt);

public record ActivitySessionResponse(
    Guid Id, string Type, DateTimeOffset StartAt, DateTimeOffset EndAt,
    int? Steps, decimal? DistanceM, decimal? Calories, DateTimeOffset CreatedAt);
