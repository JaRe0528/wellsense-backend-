using FluentAssertions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Notifications.RegisterToken;
using WellSense.Domain.Devices;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Notifications;

public class NotificationTokensTests
{
    private static Device SeedDevice(WellSense.Infrastructure.Persistence.WellSenseDbContext db, Guid userId, FixedClock clock)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(), UserId = userId, Type = DeviceType.Phone, Status = DeviceStatus.Active,
            PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Devices.Add(device);
        db.SaveChangesAsync().GetAwaiter().GetResult();
        return device;
    }

    [Fact]
    public async Task Register_token_creates_a_row()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedDevice(db, userId, clock);
        var handler = new RegisterNotificationTokenCommandHandler(db, clock);

        await handler.Handle(new RegisterNotificationTokenCommand(userId, device.Id, "fcm-token-1"), default);

        db.NotificationTokens.Should().ContainSingle(t => t.DeviceId == device.Id && t.FcmToken == "fcm-token-1");
    }

    [Fact]
    public async Task Re_registering_replaces_the_previous_token_of_the_same_device_not_adds_a_second_one()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedDevice(db, userId, clock);
        var handler = new RegisterNotificationTokenCommandHandler(db, clock);

        await handler.Handle(new RegisterNotificationTokenCommand(userId, device.Id, "fcm-token-old"), default);
        await handler.Handle(new RegisterNotificationTokenCommand(userId, device.Id, "fcm-token-new"), default);

        db.NotificationTokens.Should().ContainSingle(t => t.DeviceId == device.Id && t.FcmToken == "fcm-token-new");
    }

    [Fact]
    public async Task Registering_for_a_device_of_another_user_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var device = SeedDevice(db, owner, clock);
        var handler = new RegisterNotificationTokenCommandHandler(db, clock);

        var act = () => handler.Handle(new RegisterNotificationTokenCommand(attacker, device.Id, "fcm-token"), default);

        await act.Should().ThrowAsync<SyncDomainException>().Where(e => e.ErrorCode == "DEVICE_NOT_FOUND");
    }
}
