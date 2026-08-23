using MediatR;

namespace WellSense.Application.Memberships.SubscribeToPlan;

public record SubscribeToPlanCommand(
    Guid CurrentUserId,
    string PlanCode,
    string? PaymentMethodToken,
    string IdempotencyKey) : IRequest<SubscribeToPlanResult>;

public record SubscribeToPlanResult(
    Guid SubscriptionId,
    string PlanCode,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndsAt,
    Guid? PaymentId);
