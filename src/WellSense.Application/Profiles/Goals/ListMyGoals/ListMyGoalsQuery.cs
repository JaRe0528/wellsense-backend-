using MediatR;

namespace WellSense.Application.Profiles.Goals.ListMyGoals;

public record ListMyGoalsQuery(Guid CurrentUserId) : IRequest<IReadOnlyList<GoalResult>>;

public record GoalResult(Guid Id, string Type, decimal TargetValue, DateTimeOffset CreatedAt);
