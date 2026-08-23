using FluentAssertions;
using WellSense.Application.Notifications.SendNotification;
using WellSense.Domain.Devices;
using WellSense.Domain.Notifications;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Notifications;

public class SendNotificationCommandHandlerTests
{
    [Fact]
    public async Task Always_creates_the_in_app_notification_even_with_no_registered_tokens()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var sender = new RecordingPushNotificationSender();
        var handler = new SendNotificationCommandHandler(db, sender, clock);

        var result = await handler.Handle(new SendNotificationCommand(userId, "TEST", "Hola", "Cuerpo"), default);

        db.Notifications.Should().ContainSingle(n => n.Id == result.NotificationId && n.Title == "Hola");
        result.PushedCount.Should().Be(0);
        result.FailedPushCount.Should().Be(0);
        sender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Pushes_to_every_registered_token_of_the_user()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device1 = new Device { Id = Guid.NewGuid(), UserId = userId, Type = DeviceType.Phone, PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        var device2 = new Device { Id = Guid.NewGuid(), UserId = userId, Type = DeviceType.Watch, PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Devices.AddRange(device1, device2);
        db.NotificationTokens.Add(new NotificationToken { Id = Guid.NewGuid(), UserId = userId, DeviceId = device1.Id, FcmToken = "token-1", CreatedAt = clock.UtcNow });
        db.NotificationTokens.Add(new NotificationToken { Id = Guid.NewGuid(), UserId = userId, DeviceId = device2.Id, FcmToken = "token-2", CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var sender = new RecordingPushNotificationSender();
        var handler = new SendNotificationCommandHandler(db, sender, clock);

        var result = await handler.Handle(new SendNotificationCommand(userId, "TEST", "Hola", "Cuerpo"), default);

        result.PushedCount.Should().Be(2);
        sender.Sent.Select(s => s.Token).Should().BeEquivalentTo(["token-1", "token-2"]);
    }

    [Fact]
    public async Task Push_failures_still_leave_the_in_app_notification_persisted()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = new Device { Id = Guid.NewGuid(), UserId = userId, Type = DeviceType.Phone, PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Devices.Add(device);
        db.NotificationTokens.Add(new NotificationToken { Id = Guid.NewGuid(), UserId = userId, DeviceId = device.Id, FcmToken = "stale-token", CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var sender = new RecordingPushNotificationSender { AlwaysSucceeds = false };
        var handler = new SendNotificationCommandHandler(db, sender, clock);

        var result = await handler.Handle(new SendNotificationCommand(userId, "TEST", "Hola", "Cuerpo"), default);

        result.FailedPushCount.Should().Be(1);
        db.Notifications.Should().ContainSingle(); // el registro in-app no depende de que el push haya funcionado
    }
}
