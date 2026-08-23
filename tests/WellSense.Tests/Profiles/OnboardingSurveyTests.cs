using FluentAssertions;
using WellSense.Application.Profiles.Onboarding.GetMySurvey;
using WellSense.Application.Profiles.Onboarding.UpsertMySurvey;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Profiles;

public class OnboardingSurveyTests
{
    [Fact]
    public async Task GetMySurvey_returns_null_when_not_answered_yet()
    {
        using var db = InMemoryDbContextFactory.Create();
        var handler = new GetMyOnboardingSurveyQueryHandler(db);

        var result = await handler.Handle(new GetMyOnboardingSurveyQuery(Guid.NewGuid()), default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Upsert_creates_profile_and_survey_lazily_then_allows_resubmission()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var upsertHandler = new UpsertMyOnboardingSurveyCommandHandler(db, clock);

        await upsertHandler.Handle(new UpsertMyOnboardingSurveyCommand(
            userId, "9am-6pm", "11pm-7am", "moderate", "ALTO", "regular"), default);

        db.Profiles.Should().ContainSingle(p => p.UserId == userId);
        db.OnboardingSurveys.Should().ContainSingle();

        // Recontestar con un nivel de estrés distinto — debe actualizar, no duplicar.
        await upsertHandler.Handle(new UpsertMyOnboardingSurveyCommand(
            userId, "9am-6pm", "11pm-7am", "moderate", "BAJO", "regular"), default);

        db.OnboardingSurveys.Should().HaveCount(1);

        var getHandler = new GetMyOnboardingSurveyQueryHandler(db);
        var result = await getHandler.Handle(new GetMyOnboardingSurveyQuery(userId), default);

        result!.DeclaredStressLevel.Should().Be("BAJO");
    }
}
