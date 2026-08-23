using FluentAssertions;
using WellSense.Application.Payments.ListMyPayments;
using WellSense.Domain.Billing;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Payments;

public class ListMyPaymentsQueryHandlerTests
{
    [Fact]
    public async Task Returns_empty_list_with_no_payments()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new ListMyPaymentsQueryHandler(db);

        var result = await handler.Handle(new ListMyPaymentsQuery(Guid.NewGuid()), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_both_approved_and_declined_payments_ordered_newest_first_and_isolated_per_user()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var plan = new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Pro, Name = "Pro", PriceCents = 19900, Currency = "MXN" };
        db.MembershipPlans.Add(plan);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), UserId = userA, PlanId = plan.Id, AmountCents = 19900, Currency = "MXN",
            Status = PaymentStatus.Declined, TransactionId = "tx-1", CreatedAt = clock.UtcNow.AddMinutes(-10)
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), UserId = userA, PlanId = plan.Id, AmountCents = 19900, Currency = "MXN",
            Status = PaymentStatus.Approved, TransactionId = "tx-2", CreatedAt = clock.UtcNow
        });
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), UserId = userB, PlanId = plan.Id, AmountCents = 19900, Currency = "MXN",
            Status = PaymentStatus.Approved, TransactionId = "tx-3", CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ListMyPaymentsQueryHandler(db);
        var result = await handler.Handle(new ListMyPaymentsQuery(userA), default);

        result.Should().HaveCount(2);
        result[0].Status.Should().Be("APPROVED"); // más reciente primero
        result[1].Status.Should().Be("DECLINED");
        result.Should().OnlyContain(p => p.PlanCode == "PRO");
    }
}
