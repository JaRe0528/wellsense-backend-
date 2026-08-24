using WellSense.Domain.Wellness;

namespace WellSense.Application.Wellness.ComputeDailyScores;

/// <summary>
/// ML V1 — reglas explícitas y transparentes, NO un modelo entrenado. Cada función es
/// pura (mismo input siempre produce el mismo output, sin acceso a BD/reloj/red) a
/// propósito: así se puede probar exhaustivamente sin EF ni mocks, y cualquier bloque
/// futuro que integre el servicio de ML real (Python/FastAPI, ver
/// 01-ARQUITECTURA-Y-STACK.md) puede reemplazar estas funciones sin tocar el handler que
/// las orquesta (ComputeDailyScoresCommandHandler) — el contrato de entrada/salida no
/// cambia, solo cómo se calcula.
///
/// Decisión explícita de alcance de este bloque: el wellness score usa sueño + actividad
/// (pasos), NO frecuencia cardíaca — una "frecuencia cardíaca en reposo" confiable
/// necesita aislar períodos de baja actividad, que es un problema en sí mismo que este
/// bloque no resuelve. El stress score sí usa frecuencia cardíaca (banda de referencia
/// genérica, no personalizada) porque ahí es donde más aporta como señal simple.
/// Cualquier componente sin datos disponibles se excluye del promedio (nunca se penaliza
/// como si fuera "cero") — ver `Average` de C# sobre la lista de componentes presentes.
/// </summary>
public static class DailyScoringRules
{
    // ---------- Wellness ----------

    /// <summary>
    /// Ideal: 7-9h (420-540 min) → 100. Se degrada gradualmente fuera de ese rango,
    /// más pronunciado hacia abajo (dormir poco pesa más que dormir de más).
    /// </summary>
    public static double? SleepComponent(int? totalSleepMinutes)
    {
        if (totalSleepMinutes is null) return null;
        var m = totalSleepMinutes.Value;
        if (m <= 0) return 0;
        if (m < 300) return Lerp(m, 0, 300, 0, 40);       // 0-5h: 0-40
        if (m < 420) return Lerp(m, 300, 420, 40, 100);   // 5-7h: 40-100
        if (m <= 540) return 100;                          // 7-9h: 100
        if (m <= 600) return Lerp(m, 540, 600, 100, 70);  // 9-10h: 100-70
        return 70;                                          // >10h: 70
    }

    /// <summary>10,000 pasos/día → 100, escala lineal, tope en 100.</summary>
    public static double? ActivityComponent(decimal? totalSteps)
    {
        if (totalSteps is null) return null;
        var steps = (double)totalSteps.Value;
        if (steps <= 0) return 0;
        return Math.Min(100, steps / 10_000.0 * 100.0);
    }

    /// <summary>Promedio de los componentes CON datos — null si no hay ninguno.</summary>
    public static double? WellnessScore(double? sleepComponent, double? activityComponent)
        => Average(sleepComponent, activityComponent);

    // ---------- Stress ----------

    /// <summary>
    /// Banda de referencia GENÉRICA (no personalizada por usuario) — ver decisión de
    /// alcance en el resumen de la clase. &lt;=60 bpm promedio del día → 0 (tranquilo),
    /// &gt;=100 → 100 (elevado).
    /// </summary>
    public static double? HeartRateStressComponent(decimal? avgHeartRate)
    {
        if (avgHeartRate is null) return null;
        var hr = (double)avgHeartRate.Value;
        if (hr <= 60) return 0;
        if (hr < 70) return Lerp(hr, 60, 70, 0, 20);
        if (hr < 85) return Lerp(hr, 70, 85, 20, 60);
        if (hr < 100) return Lerp(hr, 85, 100, 60, 90);
        return 100;
    }

    /// <summary>Relación inversa del componente de sueño del wellness: dormir bien reduce estrés.</summary>
    public static double? SleepStressComponent(double? sleepWellnessComponent)
        => sleepWellnessComponent is null ? null : 100 - sleepWellnessComponent.Value;

    public static double? StressScoreValue(double? heartRateComponent, double? sleepStressComponent)
        => Average(heartRateComponent, sleepStressComponent);

    /// <summary>Corte simple en tercios de 0-100 — transparente y fácil de explicarle al usuario.</summary>
    public static StressLevel LevelFor(double score)
        => score < 34 ? StressLevel.Low : score <= 66 ? StressLevel.Medium : StressLevel.High;

    /// <summary>Proporción de componentes con datos reales — 1.0 si ambos, 0.5 si solo uno.</summary>
    public static decimal ConfidenceFor(int componentsAvailable, int componentsTotal)
        => componentsTotal == 0 ? 0m : Math.Round((decimal)componentsAvailable / componentsTotal, 2);

    // ---------- Helpers ----------

    private static double? Average(params double?[] components)
    {
        var present = components.Where(c => c.HasValue).Select(c => c!.Value).ToList();
        return present.Count == 0 ? null : present.Average();
    }

    private static double Lerp(double x, double x0, double x1, double y0, double y1)
        => y0 + (x - x0) * (y1 - y0) / (x1 - x0);
}
