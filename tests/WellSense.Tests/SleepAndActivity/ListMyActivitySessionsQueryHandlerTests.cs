using FluentAssertions;
using WellSense.Application.ActivitySessions.ListMyActivitySessions;
using WellSense.Domain.Measurements;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.SleepAndActivity;

public class ListMyActivitySessionsQueryHandlerTests
{
    [Fact]
    public async Task Returns_sessions_within_the_window_ordered_newest_first_and_isolated_per_user()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        db.ActivitySessions.Add(new ActivitySession
        {
            Id = Guid.NewGuid(), UserId = userId, Type = "RUNNING",
            StartAt = clock.UtcNow.AddDays(-2).AddMinutes(-30), EndAt = clock.UtcNow.AddDays(-2),
            Steps = 5000, DistanceM = 4200.5m, Calories = 320.0m, CreatedAt = clock.UtcNow.AddDays(-2)
        });
        db.ActivitySessions.Add(new ActivitySession
        {
            Id = Guid.NewGuid(), UserId = userId, Type = "WALKING",
            StartAt = clock.UtcNow.AddMinutes(-30), EndAt = clock.UtcNow,
            Steps = 1200, DistanceM = null, Calories = null, CreatedAt = clock.UtcNow
        });
        db.ActivitySessions.Add(new ActivitySession
        {
            Id = Guid.NewGuid(), UserId = userId, Type = "RUNNING",
            StartAt = clock.UtcNow.AddDays(-60).AddMinutes(-30), EndAt = clock.UtcNow.AddDays(-60),
            Steps = 3000, CreatedAt = clock.UtcNow.AddDays(-60)
        });
        db.ActivitySessions.Add(new ActivitySession
        {
            Id = Guid.NewGuid(), UserId = otherUserId, Type = "RUNNING",
            StartAt = clock.UtcNow.AddMinutes(-30), EndAt = clock.UtcNow, CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ListMyActivitySessionsQueryHandler(db, clock);
        var result = await handler.Handle(new ListMyActivitySessionsQuery(userId, 7), default);

        result.Should().HaveCount(2);
        result[0].Type.Should().Be("WALKING"); // más reciente primero
        result[0].DistanceM.Should().BeNull(); // nullable respetado
        result[1].Type.Should().Be("RUNNING");
        result[1].DistanceM.Should().Be(4200.5m);
    }

    [Fact]
    public async Task Default_30_day_window_excludes_sessions_older_than_30_days()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        db.ActivitySessions.Add(new ActivitySession
        {
            Id = Guid.NewGuid(), UserId = userId, Type = "RUNNING",
            StartAt = clock.UtcNow.AddDays(-45).AddMinutes(-30), EndAt = clock.UtcNow.AddDays(-45),
            CreatedAt = clock.UtcNow.AddDays(-45)
        });
        await db.SaveChangesAsync();

        var handler = new ListMyActivitySessionsQueryHandler(db, clock);
        var result = await handler.Handle(new ListMyActivitySessionsQuery(userId, 30), default);

        result.Should().BeEmpty();
    }
}
