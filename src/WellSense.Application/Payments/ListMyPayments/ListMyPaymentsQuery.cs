using MediatR;

namespace WellSense.Application.Payments.ListMyPayments;

public record ListMyPaymentsQuery(Guid CurrentUserId) : IRequest<IReadOnlyList<PaymentResult>>;

public record PaymentResult(
    Guid Id,
    string PlanCode,
    int AmountCents,
    string Currency,
    string Status,
    string? CardBrand,
    string? CardLast4,
    DateTimeOffset CreatedAt);
