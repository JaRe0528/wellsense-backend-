using FluentAssertions;
using WellSense.Application.Admin.GetUserDetail;
using WellSense.Application.Common.Exceptions;
using WellSense.Domain.Billing;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;
using WellSense.Domain.Profiles;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Admin;

public class GetUserDetailQueryHandlerTests
{
    [Fact]
    public async Task Returns_profile_devices_and_active_subscription_when_present()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User { Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.Add(user);
        db.Profiles.Add(new Profile { Id = Guid.NewGuid(), UserId = user.Id, FirstName = "Ana", Timezone = "America/Mexico_City", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow });
        db.Devices.Add(new Device { Id = Guid.NewGuid(), UserId = user.Id, Type = DeviceType.Watch, Status = DeviceStatus.Active, PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow });
        var plan = new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Pro, Name = "Pro", PriceCents = 19900, Currency = "MXN" };
        db.MembershipPlans.Add(plan);
        db.Subscriptions.Add(new Subscription { Id = Guid.NewGuid(), UserId = user.Id, PlanId = plan.Id, Status = SubscriptionStatus.Active, StartedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetUserDetailQueryHandler(db);
        var result = await handler.Handle(new GetUserDetailQuery(user.Id), default);

        result.Profile!.FirstName.Should().Be("Ana");
        result.Devices.Should().ContainSingle();
        result.Subscription!.PlanCode.Should().Be("PRO");
    }

    [Fact]
    public async Task Returns_nulls_for_profile_and_subscription_when_neither_exists()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User { Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var handler = new GetUserDetailQueryHandler(db);

        var result = await handler.Handle(new GetUserDetailQuery(user.Id), default);

        result.Profile.Should().BeNull();
        result.Subscription.Should().BeNull();
        result.Devices.Should().BeEmpty();
    }

    [Fact]
    public async Task Nonexistent_user_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new GetUserDetailQueryHandler(db);

        var act = () => handler.Handle(new GetUserDetailQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<AdminDomainException>().Where(e => e.ErrorCode == "USER_NOT_FOUND");
    }
}
