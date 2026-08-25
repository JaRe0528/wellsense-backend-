using MediatR;

namespace WellSense.Application.Admin.GetUserDetail;

public record GetUserDetailQuery(Guid TargetUserId) : IRequest<AdminUserDetail>;

public record AdminProfileSummary(string? FirstName, string? LastName, string Timezone);
public record AdminDeviceSummary(Guid Id, string Type, string Status, DateTimeOffset? LastSeenAt);
public record AdminSubscriptionSummary(string PlanCode, string Status, DateTimeOffset StartedAt, DateTimeOffset? EndsAt);

public record AdminUserDetail(
    Guid Id,
    string Email,
    bool EmailVerified,
    string Role,
    string Status,
    DateTimeOffset CreatedAt,
    AdminProfileSummary? Profile,
    IReadOnlyList<AdminDeviceSummary> Devices,
    AdminSubscriptionSummary? Subscription);
