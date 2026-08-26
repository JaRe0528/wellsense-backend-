using FluentAssertions;
using WellSense.Application.SleepSessions.ListMySleepSessions;
using WellSense.Domain.Measurements;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.SleepAndActivity;

public class ListMySleepSessionsQueryHandlerTests
{
    [Fact]
    public async Task Returns_sessions_within_the_window_ordered_newest_first_and_isolated_per_user()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        // Dentro de la ventana de 7 días, más vieja.
        db.SleepSessions.Add(new SleepSession
        {
            Id = Guid.NewGuid(), UserId = userId,
            StartAt = clock.UtcNow.AddDays(-3).AddHours(-8), EndAt = clock.UtcNow.AddDays(-3),
            Stages = "{\"deep\":90,\"light\":300}", CreatedAt = clock.UtcNow.AddDays(-3)
        });
        // Dentro de la ventana, más reciente.
        db.SleepSessions.Add(new SleepSession
        {
            Id = Guid.NewGuid(), UserId = userId,
            StartAt = clock.UtcNow.AddHours(-8), EndAt = clock.UtcNow,
            Stages = "{}", CreatedAt = clock.UtcNow
        });
        // Fuera de la ventana de 7 días.
        db.SleepSessions.Add(new SleepSession
        {
            Id = Guid.NewGuid(), UserId = userId,
            StartAt = clock.UtcNow.AddDays(-30).AddHours(-8), EndAt = clock.UtcNow.AddDays(-30),
            Stages = "{}", CreatedAt = clock.UtcNow.AddDays(-30)
        });
        // De otro usuario — nunca debe aparecer.
        db.SleepSessions.Add(new SleepSession
        {
            Id = Guid.NewGuid(), UserId = otherUserId,
            StartAt = clock.UtcNow.AddHours(-8), EndAt = clock.UtcNow,
            Stages = "{}", CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ListMySleepSessionsQueryHandler(db, clock);
        var result = await handler.Handle(new ListMySleepSessionsQuery(userId, 7), default);

        result.Should().HaveCount(2);
        result[0].EndAt.Should().Be(clock.UtcNow); // más reciente primero
        result[0].Stages.Should().Be("{}");
        result[1].Stages.Should().Be("{\"deep\":90,\"light\":300}");
    }

    [Fact]
    public async Task Duration_minutes_reflects_the_generated_column_value_as_stored()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        db.SleepSessions.Add(new SleepSession
        {
            Id = Guid.NewGuid(), UserId = userId,
            StartAt = clock.UtcNow.AddHours(-8), EndAt = clock.UtcNow,
            DurationMinutes = 480, Stages = "{}", CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ListMySleepSessionsQueryHandler(db, clock);
        var result = await handler.Handle(new ListMySleepSessionsQuery(userId, 30), default);

        result.Single().DurationMinutes.Should().Be(480);
    }
}
