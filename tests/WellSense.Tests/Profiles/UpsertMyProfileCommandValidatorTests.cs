using FluentAssertions;
using WellSense.Application.Profiles.UpsertMyProfile;
using Xunit;

namespace WellSense.Tests.Profiles;

public class UpsertMyProfileCommandValidatorTests
{
    private readonly UpsertMyProfileCommandValidator _validator = new();

    [Theory]
    [InlineData("America/Mexico_City")]
    [InlineData("UTC")]
    [InlineData("Europe/Madrid")]
    public async Task Valid_iana_timezones_pass(string tz)
    {
        var result = await _validator.ValidateAsync(
            new UpsertMyProfileCommand(Guid.NewGuid(), null, null, null, null, null, null, null, tz));

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(UpsertMyProfileCommand.Timezone));
    }

    [Theory]
    [InlineData("Not/A_Real_Zone")]
    [InlineData("")]
    [InlineData("GMT+25")]
    public async Task Invalid_timezones_fail(string tz)
    {
        var result = await _validator.ValidateAsync(
            new UpsertMyProfileCommand(Guid.NewGuid(), null, null, null, null, null, null, null, tz));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpsertMyProfileCommand.Timezone));
    }

    [Fact]
    public async Task Future_birth_date_fails()
    {
        var result = await _validator.ValidateAsync(new UpsertMyProfileCommand(
            Guid.NewGuid(), null, null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), null, null, null, null, "UTC"));

        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpsertMyProfileCommand.BirthDate));
    }
}
