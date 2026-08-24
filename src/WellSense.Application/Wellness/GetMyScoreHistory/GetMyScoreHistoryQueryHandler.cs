using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Wellness.GetMyScoreHistory;

/// <summary>
/// Últimos `Days` días terminando en "hoy" según la zona horaria del usuario (mismo
/// cálculo que ComputeDailyScoresCommandHandler) — para graficar en el dashboard. Trae
/// solo los días que SÍ tienen algún puntaje calculado (no rellena huecos con null para
/// cada día sin datos); el cliente decide cómo mostrar los días faltantes.
/// </summary>
public class GetMyScoreHistoryQueryHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<GetMyScoreHistoryQuery, IReadOnlyList<DailyScoreHistoryItem>>
{
    public async Task<IReadOnlyList<DailyScoreHistoryItem>> Handle(GetMyScoreHistoryQuery request, CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == request.CurrentUserId, ct);
        var timezone = profile?.Timezone ?? "UTC";
        var today = LocalDayRange.TodayInTimezone(clock.UtcNow, timezone);
        var fromDate = today.AddDays(-request.Days + 1);

        var wellnessScores = await db.WellnessScores
            .Where(w => w.UserId == request.CurrentUserId && w.Date >= fromDate && w.Date <= today)
            .ToListAsync(ct);
        var stressScores = await db.StressScores
            .Where(s => s.UserId == request.CurrentUserId && s.Date >= fromDate && s.Date <= today)
            .ToListAsync(ct);

        var dates = wellnessScores.Select(w => w.Date).Union(stressScores.Select(s => s.Date)).OrderBy(d => d);

        return dates.Select(date =>
        {
            var wellness = wellnessScores.FirstOrDefault(w => w.Date == date);
            var stress = stressScores.FirstOrDefault(s => s.Date == date);
            return new DailyScoreHistoryItem(
                date, wellness?.Score, stress?.Score, stress?.Level.ToString().ToUpperInvariant());
        }).ToList();
    }
}
