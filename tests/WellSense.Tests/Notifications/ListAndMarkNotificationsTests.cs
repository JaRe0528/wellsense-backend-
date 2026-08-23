using FluentAssertions;
using WellSense.Application.Notifications.ListMyNotifications;
using WellSense.Application.Notifications.MarkNotificationRead;
using WellSense.Domain.Notifications;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Notifications;

public class ListAndMarkNotificationsTests
{
    [Fact]
    public async Task List_only_returns_notifications_of_the_current_user_ordered_newest_first()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userA, Type = "TEST", Title = "Old", Body = "b", CreatedAt = clock.UtcNow.AddMinutes(-10) });
        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userA, Type = "TEST", Title = "New", Body = "b", CreatedAt = clock.UtcNow });
        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userB, Type = "TEST", Title = "NotMine", Body = "b", CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new ListMyNotificationsQueryHandler(db);
        var result = await handler.Handle(new ListMyNotificationsQuery(userA, UnreadOnly: false), default);

        result.Should().HaveCount(2);
        result[0].Title.Should().Be("New"); // más reciente primero
    }

    [Fact]
    public async Task List_unread_only_filters_out_already_read_notifications()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();

        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Type = "TEST", Title = "Read", Body = "b", ReadAt = clock.UtcNow, CreatedAt = clock.UtcNow });
        db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = userId, Type = "TEST", Title = "Unread", Body = "b", CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new ListMyNotificationsQueryHandler(db);
        var result = await handler.Handle(new ListMyNotificationsQuery(userId, UnreadOnly: true), default);

        result.Should().ContainSingle(n => n.Title == "Unread");
    }

    [Fact]
    public async Task Mark_read_sets_read_at_and_is_idempotent()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var notification = new Notification { Id = Guid.NewGuid(), UserId = userId, Type = "TEST", Title = "t", Body = "b", CreatedAt = clock.UtcNow };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var handler = new MarkNotificationReadCommandHandler(db, clock);
        await handler.Handle(new MarkNotificationReadCommand(userId, notification.Id), default);
        var firstReadAt = db.Notifications.Single().ReadAt;

        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        await handler.Handle(new MarkNotificationReadCommand(userId, notification.Id), default); // idempotente

        db.Notifications.Single().ReadAt.Should().Be(firstReadAt); // no se movió al segundo llamado
    }

    [Fact]
    public async Task Mark_read_for_notification_of_another_user_does_nothing_silently()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var notification = new Notification { Id = Guid.NewGuid(), UserId = owner, Type = "TEST", Title = "t", Body = "b", CreatedAt = clock.UtcNow };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var handler = new MarkNotificationReadCommandHandler(db, clock);
        await handler.Handle(new MarkNotificationReadCommand(attacker, notification.Id), default);

        db.Notifications.Single().ReadAt.Should().BeNull(); // sigue sin leerse
    }
}
