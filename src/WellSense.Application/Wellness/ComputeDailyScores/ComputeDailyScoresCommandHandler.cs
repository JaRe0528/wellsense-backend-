using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Measurements;
using WellSense.Domain.Wellness;

namespace WellSense.Application.Wellness.ComputeDailyScores;

/// <summary>
/// ML V1 (reglas) — consume measurements/sleep_sessions/activity_sessions de UN día
/// calendario, calculado en la zona horaria LOCAL del usuario (decisión del Bloque 3,
/// aplicada acá por primera vez vía LocalDayRange), y produce wellness_score/stress_score
/// para ese día. Recalculable: si ya existe un puntaje para esa fecha, se actualiza en
/// vez de duplicar (UNIQUE(user_id, date) en ambas tablas) — útil si llega sync tardío de
/// datos de un día ya calculado.
///
/// No hay riesgo de orden de escritura tipo Bloque 6 aquí: WellnessScore/StressScore se
/// actualizan EN LA MISMA fila si ya existe (no se reemplaza una fila "activa" por otra
/// nueva), así que una sola llamada a SaveChanges es segura y suficiente.
/// </summary>
public class ComputeDailyScoresCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<ComputeDailyScoresCommand, ComputeDailyScoresResult>
{
    private const string ModelVersion = "rules-v1";

    public async Task<ComputeDailyScoresResult> Handle(ComputeDailyScoresCommand request, CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == request.CurrentUserId, ct);
        var timezone = profile?.Timezone ?? "UTC";

        var date = request.Date ?? LocalDayRange.TodayInTimezone(clock.UtcNow, timezone);
        var (utcStart, utcEnd) = LocalDayRange.ForLocalDate(date, timezone);

        // Se materializan las listas (no se agrega en SQL) para poder distinguir "cero
        // pasos sincronizados esta noche" (dato real, componente = 0) de "no hay ningún
        // measurement ese día" (sin dato, componente = null, se excluye del promedio) —
        // Sum()/Average() de una secuencia vacía no hacen esa distinción por sí solos.
        var stepsMeasurements = await db.Measurements
            .Where(m => m.UserId == request.CurrentUserId && m.Type == MeasurementType.Steps
                && m.RecordedAt >= utcStart && m.RecordedAt < utcEnd)
            .ToListAsync(ct);
        var heartRateMeasurements = await db.Measurements
            .Where(m => m.UserId == request.CurrentUserId && m.Type == MeasurementType.HeartRate
                && m.RecordedAt >= utcStart && m.RecordedAt < utcEnd)
            .ToListAsync(ct);
        var sleepSessions = await db.SleepSessions
            .Where(s => s.UserId == request.CurrentUserId && s.EndAt >= utcStart && s.EndAt < utcEnd)
            .ToListAsync(ct);
        var activitySessionCount = await db.ActivitySessions
            .CountAsync(a => a.UserId == request.CurrentUserId && a.StartAt >= utcStart && a.StartAt < utcEnd, ct);

        decimal? totalSteps = stepsMeasurements.Count > 0 ? stepsMeasurements.Sum(m => m.Value) : null;
        decimal? avgHeartRate = heartRateMeasurements.Count > 0 ? heartRateMeasurements.Average(m => m.Value) : null;
        int? totalSleepMinutes = sleepSessions.Count > 0 ? sleepSessions.Sum(s => s.DurationMinutes) : null;

        var sleepComponent = DailyScoringRules.SleepComponent(totalSleepMinutes);
        var activityComponent = DailyScoringRules.ActivityComponent(totalSteps);
        var wellnessValue = DailyScoringRules.WellnessScore(sleepComponent, activityComponent);

        var heartRateStressComponent = DailyScoringRules.HeartRateStressComponent(avgHeartRate);
        var sleepStressComponent = DailyScoringRules.SleepStressComponent(sleepComponent);
        var stressValue = DailyScoringRules.StressScoreValue(heartRateStressComponent, sleepStressComponent);

        if (wellnessValue is null && stressValue is null)
            throw MlDomainException.InsufficientData();

        WellnessScoreDto? wellnessDto = null;
        if (wellnessValue is not null)
        {
            var wellnessRow = await db.WellnessScores
                .FirstOrDefaultAsync(w => w.UserId == request.CurrentUserId && w.Date == date, ct);
            var score = Math.Round((decimal)wellnessValue.Value, 1);
            if (wellnessRow is null)
            {
                wellnessRow = new WellnessScore
                {
                    Id = Guid.NewGuid(), UserId = request.CurrentUserId, Date = date,
                    Score = score, CreatedAt = clock.UtcNow
                };
                db.WellnessScores.Add(wellnessRow);
            }
            else
            {
                wellnessRow.Score = score;
            }
            wellnessDto = new WellnessScoreDto(score);
        }

        StressScoreDto? stressDto = null;
        if (stressValue is not null)
        {
            var componentsAvailable = new[] { heartRateStressComponent, sleepStressComponent }.Count(c => c.HasValue);
            var confidence = DailyScoringRules.ConfidenceFor(componentsAvailable, 2);
            var score = Math.Round((decimal)stressValue.Value, 1);
            var level = DailyScoringRules.LevelFor(stressValue.Value);

            var factors = JsonSerializer.Serialize(new
            {
                avgHeartRate,
                heartRateStressComponent,
                sleepStressComponent,
                totalSleepMinutes
            });

            var stressRow = await db.StressScores
                .FirstOrDefaultAsync(s => s.UserId == request.CurrentUserId && s.Date == date, ct);
            if (stressRow is null)
            {
                stressRow = new StressScore
                {
                    Id = Guid.NewGuid(), UserId = request.CurrentUserId, Date = date,
                    Score = score, Level = level, Confidence = confidence, Factors = factors,
                    CreatedAt = clock.UtcNow
                };
                db.StressScores.Add(stressRow);
            }
            else
            {
                stressRow.Score = score;
                stressRow.Level = level;
                stressRow.Confidence = confidence;
                stressRow.Factors = factors;
            }
            stressDto = new StressScoreDto(score, level.ToString().ToUpperInvariant(), confidence);
        }

        // Auditoría/trazabilidad: qué datos crudos entraron y qué salió — útil para
        // depurar "por qué mi puntaje de hoy es X" y es exactamente para lo que existe
        // ml_predictions (Bloque 1). model_version="rules-v1" deja explícito que esto es
        // el motor de reglas de este bloque, no el servicio de ML real (Python/FastAPI)
        // que en algún momento lo reemplace o complemente.
        db.MlPredictions.Add(new MlPrediction
        {
            Id = Guid.NewGuid(),
            UserId = request.CurrentUserId,
            ModelVersion = ModelVersion,
            Type = "daily_scores",
            Input = JsonSerializer.Serialize(new
            {
                date, timezone, totalSteps, avgHeartRate, totalSleepMinutes, activitySessionCount
            }),
            Output = JsonSerializer.Serialize(new
            {
                wellnessScore = wellnessDto?.Score, stressScore = stressDto?.Score, stressLevel = stressDto?.Level
            }),
            CreatedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);

        return new ComputeDailyScoresResult(date, wellnessDto, stressDto);
    }
}
