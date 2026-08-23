using FluentAssertions;
using WellSense.Application.Profiles.GetMyProfile;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Profiles;

public class GetMyProfileQueryHandlerTests
{
    [Fact]
    public async Task First_call_lazily_creates_an_empty_profile_defaulting_to_utc()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();

        var handler = new GetMyProfileQueryHandler(db, clock);
        var result = await handler.Handle(new GetMyProfileQuery(userId), default);

        result.Timezone.Should().Be("UTC");
        result.FirstName.Should().BeNull();
        db.Profiles.Should().ContainSingle(p => p.UserId == userId);
    }

    [Fact]
    public async Task Second_call_returns_the_same_profile_not_a_new_one()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var handler = new GetMyProfileQueryHandler(db, clock);

        var first = await handler.Handle(new GetMyProfileQuery(userId), default);
        var second = await handler.Handle(new GetMyProfileQuery(userId), default);

        second.Id.Should().Be(first.Id);
        db.Profiles.Should().HaveCount(1);
    }
}
