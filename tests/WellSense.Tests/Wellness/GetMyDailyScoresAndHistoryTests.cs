using FluentAssertions;
using WellSense.Application.Wellness.GetMyDailyScores;
using WellSense.Application.Wellness.GetMyScoreHistory;
using WellSense.Domain.Profiles;
using WellSense.Domain.Wellness;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Wellness;

public class GetMyDailyScoresAndHistoryTests
{
    [Fact]
    public async Task GetMyDailyScores_returns_nulls_when_nothing_computed_yet()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new GetMyDailyScoresQueryHandler(db, new FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new GetMyDailyScoresQuery(Guid.NewGuid(), new DateOnly(2026, 8, 23)), default);

        result.WellnessScore.Should().BeNull();
        result.StressScore.Should().BeNull();
    }

    [Fact]
    public async Task GetMyDailyScores_without_date_resolves_today_in_the_users_local_timezone()
    {
        using var db = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 24, 4, 0, 0, TimeSpan.Zero)); // 22:00 del 23 en CDMX
        db.Profiles.Add(new Profile { Id = Guid.NewGuid(), UserId = userId, Timezone = "America/Mexico_City", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow });
        db.WellnessScores.Add(new WellnessScore { Id = Guid.NewGuid(), UserId = userId, Date = new DateOnly(2026, 8, 23), Score = 77, CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetMyDailyScoresQueryHandler(db, clock);
        var result = await handler.Handle(new GetMyDailyScoresQuery(userId, null), default);

        result.Date.Should().Be(new DateOnly(2026, 8, 23));
        result.WellnessScore.Should().Be(77);
    }

    [Fact]
    public async Task GetMyScoreHistory_returns_only_days_that_have_a_computed_score()
    {
        using var db = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        db.WellnessScores.Add(new WellnessScore { Id = Guid.NewGuid(), UserId = userId, Date = new DateOnly(2026, 8, 21), Score = 60, CreatedAt = clock.UtcNow });
        db.WellnessScores.Add(new WellnessScore { Id = Guid.NewGuid(), UserId = userId, Date = new DateOnly(2026, 8, 23), Score = 80, CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetMyScoreHistoryQueryHandler(db, clock);
        var result = await handler.Handle(new GetMyScoreHistoryQuery(userId, 7), default);

        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Date == new DateOnly(2026, 8, 21) && r.WellnessScore == 60);
        result.Should().Contain(r => r.Date == new DateOnly(2026, 8, 23) && r.WellnessScore == 80);
    }

    [Fact]
    public async Task GetMyScoreHistory_isolates_between_users()
    {
        using var db = InMemoryDbContextFactory.Create();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        db.WellnessScores.Add(new WellnessScore { Id = Guid.NewGuid(), UserId = userA, Date = new DateOnly(2026, 8, 23), Score = 60, CreatedAt = clock.UtcNow });
        db.WellnessScores.Add(new WellnessScore { Id = Guid.NewGuid(), UserId = userB, Date = new DateOnly(2026, 8, 23), Score = 90, CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetMyScoreHistoryQueryHandler(db, clock);
        var result = await handler.Handle(new GetMyScoreHistoryQuery(userA, 7), default);

        result.Should().ContainSingle(r => r.WellnessScore == 60);
    }
}
