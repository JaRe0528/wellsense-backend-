using MediatR;

namespace WellSense.Application.Wellness.GetMyScoreHistory;

public record GetMyScoreHistoryQuery(Guid CurrentUserId, int Days) : IRequest<IReadOnlyList<DailyScoreHistoryItem>>;

public record DailyScoreHistoryItem(DateOnly Date, decimal? WellnessScore, decimal? StressScore, string? StressLevel);
