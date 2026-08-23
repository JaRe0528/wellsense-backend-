using FluentAssertions;
using WellSense.Application.Profiles.UpsertMyProfile;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Profiles;

public class UpsertMyProfileCommandHandlerTests
{
    [Fact]
    public async Task Creates_profile_on_first_upsert_and_updates_on_second()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var handler = new UpsertMyProfileCommandHandler(db, clock);

        await handler.Handle(new UpsertMyProfileCommand(
            userId, "Ana", "Pérez", new DateOnly(1990, 5, 1), 60, 165, "Ingeniera", null, "America/Mexico_City"), default);

        db.Profiles.Should().ContainSingle(p => p.UserId == userId && p.FirstName == "Ana" && p.Timezone == "America/Mexico_City");

        await handler.Handle(new UpsertMyProfileCommand(
            userId, "Ana", "Gómez", new DateOnly(1990, 5, 1), 61, 165, "Ingeniera Sr.", null, "America/Mexico_City"), default);

        db.Profiles.Should().HaveCount(1);
        db.Profiles.Single().LastName.Should().Be("Gómez");
        db.Profiles.Single().WeightKg.Should().Be(61);
    }
}
