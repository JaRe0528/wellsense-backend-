namespace WellSense.Api.Contracts;

public record PaymentResponse(
    Guid Id, string PlanCode, int AmountCents, string Currency, string Status,
    string? CardBrand, string? CardLast4, DateTimeOffset CreatedAt);
