using FluentAssertions;
using WellSense.Application.Common;
using Xunit;

namespace WellSense.Tests.Wellness;

/// <summary>
/// Prueba directa de la decisión de zona horaria del Bloque 3 — el caso que realmente
/// importa es "UTC ya cambió de día mientras el usuario, en su zona local, todavía no".
/// </summary>
public class LocalDayRangeTests
{
    private const string MexicoCity = "America/Mexico_City"; // UTC-6 fijo (México abolió el horario de verano en 2022)

    [Fact]
    public void ForLocalDate_converts_local_midnight_to_midnight_to_the_correct_utc_range()
    {
        var (start, end) = LocalDayRange.ForLocalDate(new DateOnly(2026, 8, 23), MexicoCity);

        // Medianoche local del 23 = 06:00 UTC del mismo día (UTC = local + 6h en CDMX)
        start.Should().Be(new DateTimeOffset(2026, 8, 23, 6, 0, 0, TimeSpan.Zero));
        end.Should().Be(new DateTimeOffset(2026, 8, 24, 6, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void TodayInTimezone_stays_on_the_previous_local_day_even_after_utc_has_already_rolled_over()
    {
        // Exactamente el caso que motivó la decisión del Bloque 3: son las 22:00 en CDMX
        // (23 de agosto), pero en UTC ya son las 04:00 del 24 de agosto — un cálculo
        // ingenuo por fecha UTC diría "24", cuando para el usuario todavía es "23".
        var utcNow = new DateTimeOffset(2026, 8, 24, 4, 0, 0, TimeSpan.Zero);

        var today = LocalDayRange.TodayInTimezone(utcNow, MexicoCity);

        today.Should().Be(new DateOnly(2026, 8, 23));
    }

    [Fact]
    public void TodayInTimezone_for_utc_matches_the_utc_date_directly()
    {
        var utcNow = new DateTimeOffset(2026, 8, 24, 4, 0, 0, TimeSpan.Zero);

        var today = LocalDayRange.TodayInTimezone(utcNow, "UTC");

        today.Should().Be(new DateOnly(2026, 8, 24));
    }

    [Fact]
    public void Invalid_timezone_string_falls_back_to_utc_instead_of_throwing()
    {
        var act = () => LocalDayRange.ForLocalDate(new DateOnly(2026, 8, 23), "Not/A_Real_Zone");

        act.Should().NotThrow();
        var (start, _) = LocalDayRange.ForLocalDate(new DateOnly(2026, 8, 23), "Not/A_Real_Zone");
        start.Should().Be(new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.Zero)); // cayó a UTC
    }
}
