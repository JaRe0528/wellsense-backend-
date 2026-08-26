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

    [Fact]
    public async Task History_is_clamped_to_the_users_plan_limit_even_when_more_days_are_requested()
    {
        using var db = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        var plan = new WellSense.Domain.Billing.MembershipPlan
        {
            Id = Guid.NewGuid(), Code = WellSense.Domain.Billing.PlanCode.Free, Name = "Free",
            PriceCents = 0, Currency = "MXN", Limits = "{\"maxDevices\": 1, \"historyDays\": 2}"
        };
        db.MembershipPlans.Add(plan);
        db.Subscriptions.Add(new WellSense.Domain.Billing.Subscription
        {
            Id = Guid.NewGuid(), UserId = userId, PlanId = plan.Id,
            Status = WellSense.Domain.Billing.SubscriptionStatus.Active, StartedAt = clock.UtcNow
        });
        // Un puntaje DENTRO del límite de 2 días (hoy) y otro FUERA (hace 5 días).
        db.WellnessScores.Add(new WellnessScore { Id = Guid.NewGuid(), UserId = userId, Date = new DateOnly(2026, 8, 23), Score = 80, CreatedAt = clock.UtcNow });
        db.WellnessScores.Add(new WellnessScore { Id = Guid.NewGuid(), UserId = userId, Date = new DateOnly(2026, 8, 18), Score = 50, CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetMyScoreHistoryQueryHandler(db, clock);
        // El cliente pide 30 días — el plan (FREE, historyDays=2) debe recortarlo, NUNCA dar error.
        var result = await handler.Handle(new GetMyScoreHistoryQuery(userId, 30), default);

        result.Should().ContainSingle(r => r.WellnessScore == 80);
        result.Should().NotContain(r => r.WellnessScore == 50); // fuera del límite de 2 días, recortado en silencio
    }

    [Fact]
    public async Task Requesting_fewer_days_than_the_plan_allows_is_never_expanded()
    {
        using var db = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        var plan = new WellSense.Domain.Billing.MembershipPlan
        {
            Id = Guid.NewGuid(), Code = WellSense.Domain.Billing.PlanCode.Professional, Name = "Professional",
            PriceCents = 39900, Currency = "MXN", Limits = "{\"maxDevices\": null, \"historyDays\": null}"
        };
        db.MembershipPlans.Add(plan);
        db.Subscriptions.Add(new WellSense.Domain.Billing.Subscription
        {
            Id = Guid.NewGuid(), UserId = userId, PlanId = plan.Id,
            Status = WellSense.Domain.Billing.SubscriptionStatus.Active, StartedAt = clock.UtcNow
        });
        db.WellnessScores.Add(new WellnessScore { Id = Guid.NewGuid(), UserId = userId, Date = new DateOnly(2026, 8, 10), Score = 70, CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetMyScoreHistoryQueryHandler(db, clock);
        // Plan ilimitado, pero el cliente solo pidió 1 día — no debe "regalar" más de lo pedido.
        var result = await handler.Handle(new GetMyScoreHistoryQuery(userId, 1), default);

        result.Should().BeEmpty(); // el puntaje del 10 de agosto queda fuera del único día pedido
    }
}
