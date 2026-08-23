using FluentAssertions;
using WellSense.Application.Memberships.GetMyMembership;
using WellSense.Application.Memberships.ListPlans;
using WellSense.Domain.Billing;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Memberships;

public class ListPlansAndGetMyMembershipTests
{
    private static async Task SeedPlansAsync(WellSense.Infrastructure.Persistence.WellSenseDbContext db)
    {
        db.MembershipPlans.AddRange(
            new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Free, Name = "Free", PriceCents = 0, Currency = "MXN" },
            new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Pro, Name = "Pro", PriceCents = 19900, Currency = "MXN" }
        );
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ListPlans_returns_all_plans_ordered_by_price()
    {
        using var db = InMemoryDbContextFactory.Create();
        await SeedPlansAsync(db);
        var handler = new ListPlansQueryHandler(db);

        var plans = await handler.Handle(new ListPlansQuery(), default);

        plans.Should().HaveCount(2);
        plans[0].Code.Should().Be("FREE");
        plans[1].Code.Should().Be("PRO");
    }

    [Fact]
    public async Task GetMyMembership_lazily_creates_a_free_subscription_on_first_call()
    {
        using var db = InMemoryDbContextFactory.Create();
        await SeedPlansAsync(db);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var handler = new GetMyMembershipQueryHandler(db, clock);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new GetMyMembershipQuery(userId), default);

        result.PlanCode.Should().Be("FREE");
        result.Status.Should().Be("ACTIVE");
        db.Subscriptions.Should().ContainSingle(s => s.UserId == userId);
    }

    [Fact]
    public async Task GetMyMembership_second_call_returns_the_same_subscription_not_a_new_one()
    {
        using var db = InMemoryDbContextFactory.Create();
        await SeedPlansAsync(db);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var handler = new GetMyMembershipQueryHandler(db, clock);
        var userId = Guid.NewGuid();

        var first = await handler.Handle(new GetMyMembershipQuery(userId), default);
        var second = await handler.Handle(new GetMyMembershipQuery(userId), default);

        second.SubscriptionId.Should().Be(first.SubscriptionId);
        db.Subscriptions.Should().HaveCount(1);
    }
}
