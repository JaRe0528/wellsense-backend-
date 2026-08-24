using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Wellness.GetMyDailyScores;

/// <summary>
/// Nunca 404 — devuelve campos null si ese día todavía no se calculó (o nunca se pudo
/// calcular por falta de datos), consistente con el resto de la Api para recursos
/// "por día"/"por usuario" que pueden legítimamente no existir aún.
///
/// Sin `Date`, resuelve "hoy" en la zona horaria LOCAL del usuario (mismo cálculo que
/// ComputeDailyScoresCommandHandler, vía LocalDayRange) — deliberado: la resolución de
/// "hoy" vive acá, en el handler, y no en el controller, para que no exista la
/// posibilidad de que un default distinto (ej. "hoy" en UTC) se cuele en la capa de Api
/// y quede inconsistente con lo que /compute considera "hoy" para el mismo usuario.
/// </summary>
public class GetMyDailyScoresQueryHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<GetMyDailyScoresQuery, DailyScoresResult>
{
    public async Task<DailyScoresResult> Handle(GetMyDailyScoresQuery request, CancellationToken ct)
    {
        DateOnly date;
        if (request.Date is not null)
        {
            date = request.Date.Value;
        }
        else
        {
            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == request.CurrentUserId, ct);
            date = LocalDayRange.TodayInTimezone(clock.UtcNow, profile?.Timezone ?? "UTC");
        }

        var wellness = await db.WellnessScores
            .Where(w => w.UserId == request.CurrentUserId && w.Date == date)
            .Select(w => (decimal?)w.Score)
            .FirstOrDefaultAsync(ct);

        var stress = await db.StressScores
            .Where(s => s.UserId == request.CurrentUserId && s.Date == date)
            .FirstOrDefaultAsync(ct);

        return new DailyScoresResult(
            date,
            wellness,
            stress?.Score,
            stress?.Level.ToString().ToUpperInvariant(),
            stress?.Confidence);
    }
}
