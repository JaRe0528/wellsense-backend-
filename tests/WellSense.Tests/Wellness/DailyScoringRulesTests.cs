using FluentAssertions;
using WellSense.Application.Wellness.ComputeDailyScores;
using WellSense.Domain.Wellness;
using Xunit;

namespace WellSense.Tests.Wellness;

public class DailyScoringRulesTests
{
    [Fact]
    public void SleepComponent_returns_null_with_no_data()
        => DailyScoringRules.SleepComponent(null).Should().BeNull();

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(480, 100.0)]  // 8h, dentro del ideal 7-9h
    [InlineData(420, 100.0)]  // exactamente 7h
    [InlineData(540, 100.0)]  // exactamente 9h
    public void SleepComponent_scores_ideal_range_at_100(int minutes, double expected)
        => DailyScoringRules.SleepComponent(minutes).Should().Be(expected);

    [Fact]
    public void SleepComponent_penalizes_too_little_sleep_more_than_too_much()
    {
        var tooLittle = DailyScoringRules.SleepComponent(240);  // 4h
        var tooMuch = DailyScoringRules.SleepComponent(660);    // 11h

        tooLittle.Should().BeLessThan(tooMuch!.Value);
    }

    [Fact]
    public void ActivityComponent_caps_at_100_for_10000_or_more_steps()
    {
        DailyScoringRules.ActivityComponent(10_000).Should().Be(100);
        DailyScoringRules.ActivityComponent(15_000).Should().Be(100);
    }

    [Fact]
    public void ActivityComponent_scales_linearly_below_10000()
        => DailyScoringRules.ActivityComponent(5_000).Should().Be(50);

    [Fact]
    public void WellnessScore_averages_only_the_available_components()
    {
        DailyScoringRules.WellnessScore(80, 60).Should().Be(70);
        DailyScoringRules.WellnessScore(80, null).Should().Be(80); // no se penaliza por falta de dato de actividad
        DailyScoringRules.WellnessScore(null, null).Should().BeNull();
    }

    [Fact]
    public void HeartRateStressComponent_low_resting_rate_scores_near_zero()
        => DailyScoringRules.HeartRateStressComponent(58).Should().Be(0);

    [Fact]
    public void HeartRateStressComponent_elevated_rate_scores_high()
        => DailyScoringRules.HeartRateStressComponent(105).Should().Be(100);

    [Fact]
    public void SleepStressComponent_is_inverse_of_sleep_wellness_component()
    {
        var sleepWellness = DailyScoringRules.SleepComponent(480); // 100 (8h ideal)
        DailyScoringRules.SleepStressComponent(sleepWellness).Should().Be(0);
    }

    [Theory]
    [InlineData(10.0, StressLevel.Low)]
    [InlineData(50.0, StressLevel.Medium)]
    [InlineData(90.0, StressLevel.High)]
    public void LevelFor_splits_into_thirds(double score, StressLevel expected)
        => DailyScoringRules.LevelFor(score).Should().Be(expected);

    [Fact]
    public void ConfidenceFor_reflects_proportion_of_available_components()
    {
        DailyScoringRules.ConfidenceFor(2, 2).Should().Be(1.0m);
        DailyScoringRules.ConfidenceFor(1, 2).Should().Be(0.5m);
        DailyScoringRules.ConfidenceFor(0, 2).Should().Be(0m);
    }
}
