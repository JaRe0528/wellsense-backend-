namespace WellSense.Application.Common;

/// <summary>
/// Implementación directa de la decisión de zona horaria del Bloque 3: el "día" para
/// wellness_scores/stress_scores se calcula en la zona horaria LOCAL del usuario
/// (profiles.timezone), no en UTC — ver HANDOFF de Bloque 3 para la justificación
/// completa. Cualquier código que agrupe measurements/sleep_sessions/activity_sessions
/// "por día" debe pasar por aquí, nunca truncar recorded_at/start_at a fecha UTC
/// directamente.
///
/// Funciones puras (determinísticas, sin I/O) a propósito — fácil de probar
/// exhaustivamente sin mocks, incluyendo transiciones de horario de verano, que
/// `TimeZoneInfo.ConvertTimeToUtc` ya resuelve correctamente usando la base de datos
/// tzdata (misma que usa Postgres) — no hace falta calcular DST a mano.
/// </summary>
public static class LocalDayRange
{
    /// <summary>Rango UTC [inicio, fin) que corresponde a la medianoche-a-medianoche LOCAL de `localDate` en `timezoneId`.</summary>
    public static (DateTimeOffset UtcStart, DateTimeOffset UtcEnd) ForLocalDate(DateOnly localDate, string timezoneId)
    {
        var tz = ResolveTimezone(timezoneId);
        var localStart = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(localDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);

        return (new DateTimeOffset(utcStart, TimeSpan.Zero), new DateTimeOffset(utcEnd, TimeSpan.Zero));
    }

    /// <summary>La fecha calendario LOCAL correspondiente a un instante UTC — para resolver "hoy" por defecto.</summary>
    public static DateOnly TodayInTimezone(DateTimeOffset utcNow, string timezoneId)
    {
        var tz = ResolveTimezone(timezoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        return DateOnly.FromDateTime(local.DateTime);
    }

    /// <summary>
    /// Nunca lanza — un `profiles.timezone` inválido (no debería pasar, UpsertMyProfile
    /// ya lo valida desde Bloque 3, pero perfiles creados antes de esa validación o
    /// datos corruptos son un caso borde real) cae a UTC en vez de tumbar el cálculo de
    /// scores completo.
    /// </summary>
    private static TimeZoneInfo ResolveTimezone(string timezoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
