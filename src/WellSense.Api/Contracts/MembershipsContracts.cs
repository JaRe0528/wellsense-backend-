namespace WellSense.Api.Contracts;

public record PlanLimitsResponse(int? MaxDevices, int? HistoryDays);
public record PlanResponse(Guid Id, string Code, string Name, int PriceCents, string Currency, IReadOnlyList<string> Features, PlanLimitsResponse Limits);

public record MembershipResponse(
    Guid SubscriptionId, string PlanCode, string PlanName, string Status, DateTimeOffset StartedAt, DateTimeOffset? EndsAt);

public record SubscribeRequest(string PlanCode, string? PaymentMethodToken, string IdempotencyKey);

public record SubscribeResponse(
    Guid SubscriptionId, string PlanCode, string Status, DateTimeOffset StartedAt, DateTimeOffset? EndsAt, Guid? PaymentId);
