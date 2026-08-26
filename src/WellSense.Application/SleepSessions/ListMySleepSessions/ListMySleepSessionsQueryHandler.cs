using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.SleepSessions.ListMySleepSessions;

/// <summary>
/// Ventana simple "últimos N días desde ahora" (UTC) — a propósito, NO alineada a
/// medianoche local como `/wellness/me/history` (Bloque 7): este endpoint lista sesiones
/// individuales con su propio start_at/end_at real, no agrega por día calendario, así
/// que no hay una noción de "día" que alinear a la zona horaria del usuario aquí.
///
/// No aplica el límite de historial por plan (`membership_plans.limits.historyDays`,
/// Parte 3 post-Bloque-10) — no se pidió para este endpoint. Si se quiere consistencia
/// con `/wellness/me/history`, es un cambio futuro explícito, no algo que se deba asumir.
/// </summary>
public class ListMySleepSessionsQueryHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<ListMySleepSessionsQuery, IReadOnlyList<SleepSessionResult>>
{
    public async Task<IReadOnlyList<SleepSessionResult>> Handle(ListMySleepSessionsQuery request, CancellationToken ct)
    {
        var since = clock.UtcNow.AddDays(-request.Days);

        var sessions = await db.SleepSessions
            .Where(s => s.UserId == request.CurrentUserId && s.StartAt >= since)
            .OrderByDescending(s => s.StartAt)
            .ToListAsync(ct);

        return sessions
            .Select(s => new SleepSessionResult(s.Id, s.StartAt, s.EndAt, s.DurationMinutes, s.Stages, s.CreatedAt))
            .ToList();
    }
}
