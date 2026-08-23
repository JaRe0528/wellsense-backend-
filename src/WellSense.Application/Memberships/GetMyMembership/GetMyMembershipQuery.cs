using MediatR;

namespace WellSense.Application.Memberships.GetMyMembership;

public record GetMyMembershipQuery(Guid CurrentUserId) : IRequest<MembershipResult>;

public record MembershipResult(
    Guid SubscriptionId,
    string PlanCode,
    string PlanName,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndsAt);
