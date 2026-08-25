namespace WellSense.Api.Contracts;

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record AdminUserSummaryResponse(Guid Id, string Email, bool EmailVerified, string Role, string Status, DateTimeOffset CreatedAt);

public record AdminProfileSummaryResponse(string? FirstName, string? LastName, string Timezone);
public record AdminDeviceSummaryResponse(Guid Id, string Type, string Status, DateTimeOffset? LastSeenAt);
public record AdminSubscriptionSummaryResponse(string PlanCode, string Status, DateTimeOffset StartedAt, DateTimeOffset? EndsAt);

public record AdminUserDetailResponse(
    Guid Id, string Email, bool EmailVerified, string Role, string Status, DateTimeOffset CreatedAt,
    AdminProfileSummaryResponse? Profile, IReadOnlyList<AdminDeviceSummaryResponse> Devices, AdminSubscriptionSummaryResponse? Subscription);

public record UpdateUserStatusRequest(string Status);

public record AdminSubscriptionListItemResponse(Guid SubscriptionId, string UserEmail, string PlanCode, DateTimeOffset StartedAt, DateTimeOffset? EndsAt);

public record PlanDistributionItemResponse(string PlanCode, int UserCount);
public record AdminStatsResponse(int TotalUsers, int ActiveUsersLast7Days, IReadOnlyList<PlanDistributionItemResponse> UsersByPlan);

public record BootstrapAdminRequest(string Secret);

public record AuditLogItemResponse(Guid Id, Guid? UserId, string? UserEmail, string Action, string Metadata, string? IpAddress, DateTimeOffset CreatedAt);
