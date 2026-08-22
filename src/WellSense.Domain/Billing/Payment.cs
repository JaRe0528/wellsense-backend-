namespace WellSense.Domain.Billing;

public enum PaymentStatus { Approved, Declined }

public class Payment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; } // plan intentado, incluso si el pago falla
    public Guid? SubscriptionId { get; set; } // solo si Status == Approved
    public int AmountCents { get; set; }
    public string Currency { get; set; } = default!;
    public PaymentStatus Status { get; set; }
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public string TransactionId { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
