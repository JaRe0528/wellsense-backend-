using FluentAssertions;
using WellSense.Application.Common;
using Xunit;

namespace WellSense.Tests.Common;

public class PlanLimitsTests
{
    [Fact]
    public void Parses_real_limits_from_json()
    {
        var limits = PlanLimits.Parse("{\"maxDevices\": 2, \"historyDays\": 30}");

        limits.MaxDevices.Should().Be(2);
        limits.HistoryDays.Should().Be(30);
    }

    [Fact]
    public void Null_values_mean_unlimited()
    {
        var limits = PlanLimits.Parse("{\"maxDevices\": null, \"historyDays\": null}");

        limits.MaxDevices.Should().BeNull();
        limits.HistoryDays.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("not valid json at all")]
    public void Empty_or_malformed_json_falls_back_to_unlimited_never_throws(string input)
    {
        var act = () => PlanLimits.Parse(input);

        act.Should().NotThrow();
        var limits = PlanLimits.Parse(input);
        limits.Should().Be(PlanLimits.Unlimited);
    }
}
