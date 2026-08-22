namespace WellSense.Domain.Billing;

public enum PlanCode { Free, Basic, Pro, Professional }

public class MembershipPlan
{
    public Guid Id { get; set; }
    public PlanCode Code { get; set; }
    public string Name { get; set; } = default!;
    public int PriceCents { get; set; }
    public string Currency { get; set; } = "MXN";
    public string Features { get; set; } = "{}"; // jsonb
}
