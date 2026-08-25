using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Billing;

namespace WellSense.Application.Admin.GetStats;

/// <summary>
/// Decisión de este bloque: "activo" se define como "tiene al menos un dispositivo con
/// actividad (last_seen_at) en los últimos 7 días" — no "inició sesión en los últimos 7
/// días" (que hubiera sido un proxy más flojo basado en refresh_tokens.created_at, que
/// incluye simples renovaciones de token sin uso real de la app). Para una app de
/// bienestar, actividad de dispositivo (sync/heartbeat) es la señal de compromiso real,
/// no solo tener una sesión web abierta.
///
/// La distribución por plan se calcula SOLO sobre filas de `subscriptions` que ya
/// existen — un usuario que nunca llamó GET /memberships/me (que crea la suscripción
/// FREE de forma perezosa, Bloque 6) simplemente no aparece en ningún plan todavía; no
/// se asume FREE para usuarios sin fila de suscripción, para no reportar un número
/// inferido en vez de un estado real y verificado.
/// </summary>
public class GetStatsQueryHandler(IWellSenseDbContext db, IDateTimeProvider clock) : IRequestHandler<GetStatsQuery, AdminStatsResult>
{
    public async Task<AdminStatsResult> Handle(GetStatsQuery request, CancellationToken ct)
    {
        var totalUsers = await db.Users.CountAsync(u => !u.IsDeleted, ct);

        var sevenDaysAgo = clock.UtcNow.AddDays(-7);
        var activeUserIds = await db.Devices
            .Where(d => d.LastSeenAt != null && d.LastSeenAt >= sevenDaysAgo)
            .Select(d => d.UserId)
            .Distinct()
            .ToListAsync(ct);
        var activeUsersLast7Days = await db.Users.CountAsync(u => !u.IsDeleted && activeUserIds.Contains(u.Id), ct);

        var activeSubscriptions = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .ToListAsync(ct);
        var plans = await db.MembershipPlans.ToDictionaryAsync(p => p.Id, ct);

        var usersByPlan = activeSubscriptions
            .GroupBy(s => plans[s.PlanId].Code.ToString().ToUpperInvariant())
            .Select(g => new PlanDistributionItem(g.Key, g.Count()))
            .OrderByDescending(p => p.UserCount)
            .ToList();

        return new AdminStatsResult(totalUsers, activeUsersLast7Days, usersByPlan);
    }
}
