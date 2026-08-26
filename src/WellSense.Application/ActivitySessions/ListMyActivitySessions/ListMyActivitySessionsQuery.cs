using MediatR;

namespace WellSense.Application.ActivitySessions.ListMyActivitySessions;

public record ListMyActivitySessionsQuery(Guid CurrentUserId, int Days) : IRequest<IReadOnlyList<ActivitySessionResult>>;

public record ActivitySessionResult(
    Guid Id, string Type, DateTimeOffset StartAt, DateTimeOffset EndAt,
    int? Steps, decimal? DistanceM, decimal? Calories, DateTimeOffset CreatedAt);
