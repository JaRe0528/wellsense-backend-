using MediatR;

namespace WellSense.Application.Wellness.ComputeDailyScores;

public record ComputeDailyScoresCommand(Guid CurrentUserId, DateOnly? Date) : IRequest<ComputeDailyScoresResult>;

public record WellnessScoreDto(decimal Score);
public record StressScoreDto(decimal Score, string Level, decimal Confidence);

public record ComputeDailyScoresResult(DateOnly Date, WellnessScoreDto? Wellness, StressScoreDto? Stress);
