namespace WellSense.Domain.Billing;

public enum SubscriptionStatus { Active, Canceled, Expired }

public class Subscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
}
