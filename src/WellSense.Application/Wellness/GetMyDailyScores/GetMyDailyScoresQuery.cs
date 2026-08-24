using MediatR;

namespace WellSense.Application.Wellness.GetMyDailyScores;

public record GetMyDailyScoresQuery(Guid CurrentUserId, DateOnly? Date) : IRequest<DailyScoresResult>;

public record DailyScoresResult(
    DateOnly Date,
    decimal? WellnessScore,
    decimal? StressScore,
    string? StressLevel,
    decimal? StressConfidence);
