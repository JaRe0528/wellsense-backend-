namespace WellSense.Api.Contracts;

public record ComputeScoresRequest(DateOnly? Date);

public record WellnessScoreResponse(decimal Score);
public record StressScoreResponse(decimal Score, string Level, decimal Confidence);

public record ComputeScoresResponse(DateOnly Date, WellnessScoreResponse? Wellness, StressScoreResponse? Stress);

public record DailyScoresResponse(
    DateOnly Date, decimal? WellnessScore, decimal? StressScore, string? StressLevel, decimal? StressConfidence);

public record DailyScoreHistoryItemResponse(DateOnly Date, decimal? WellnessScore, decimal? StressScore, string? StressLevel);
