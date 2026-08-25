using FluentAssertions;
using WellSense.Application.Admin.GetStats;
using WellSense.Domain.Billing;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Admin;

public class GetStatsQueryHandlerTests
{
    [Fact]
    public async Task Counts_total_users_and_active_users_by_device_activity_in_the_last_7_days()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var recentlyActiveUser = new User { Id = Guid.NewGuid(), Email = "a@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        var staleUser = new User { Id = Guid.NewGuid(), Email = "b@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.AddRange(recentlyActiveUser, staleUser);

        db.Devices.Add(new Device { Id = Guid.NewGuid(), UserId = recentlyActiveUser.Id, Type = DeviceType.Watch, LastSeenAt = clock.UtcNow.AddDays(-1), PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow });
        db.Devices.Add(new Device { Id = Guid.NewGuid(), UserId = staleUser.Id, Type = DeviceType.Watch, LastSeenAt = clock.UtcNow.AddDays(-30), PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetStatsQueryHandler(db, clock);
        var result = await handler.Handle(new GetStatsQuery(), default);

        result.TotalUsers.Should().Be(2);
        result.ActiveUsersLast7Days.Should().Be(1); // solo el que tuvo actividad de dispositivo hace 1 día, no hace 30
    }

    [Fact]
    public async Task Groups_active_subscriptions_by_plan_code()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var freePlan = new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Free, Name = "Free", PriceCents = 0, Currency = "MXN" };
        var proPlan = new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Pro, Name = "Pro", PriceCents = 19900, Currency = "MXN" };
        db.MembershipPlans.AddRange(freePlan, proPlan);
        var user1 = new User { Id = Guid.NewGuid(), Email = "a@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        var user2 = new User { Id = Guid.NewGuid(), Email = "b@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.AddRange(user1, user2);
        db.Subscriptions.Add(new Subscription { Id = Guid.NewGuid(), UserId = user1.Id, PlanId = proPlan.Id, Status = SubscriptionStatus.Active, StartedAt = clock.UtcNow });
        db.Subscriptions.Add(new Subscription { Id = Guid.NewGuid(), UserId = user2.Id, PlanId = freePlan.Id, Status = SubscriptionStatus.Active, StartedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetStatsQueryHandler(db, clock);
        var result = await handler.Handle(new GetStatsQuery(), default);

        result.UsersByPlan.Should().Contain(p => p.PlanCode == "PRO" && p.UserCount == 1);
        result.UsersByPlan.Should().Contain(p => p.PlanCode == "FREE" && p.UserCount == 1);
    }
}
