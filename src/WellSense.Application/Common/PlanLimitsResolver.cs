using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Billing;

namespace WellSense.Application.Common;

/// <summary>
/// Resuelve los límites efectivos del plan ACTUAL de un usuario — usado tanto por
/// RegisterDeviceCommandHandler (máximo de dispositivos) como por
/// GetMyScoreHistoryQueryHandler (días de historial). Sin suscripción activa (un usuario
/// que nunca llamó GET /memberships/me, Bloque 6) se trata como FREE, mismo criterio que
/// GetMyMembershipQueryHandler — pero SIN crear la fila de suscripción perezosamente:
/// un chequeo de límite es una lectura, no debe tener el efecto secundario de "afiliar"
/// a alguien a FREE solo por intentar registrar un dispositivo.
/// </summary>
public static class PlanLimitsResolver
{
    public static async Task<PlanLimits> ResolveForUserAsync(this IWellSenseDbContext db, Guid userId, CancellationToken ct)
    {
        var activeSubscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, ct);

        var plan = activeSubscription is not null
            ? await db.MembershipPlans.FirstAsync(p => p.Id == activeSubscription.PlanId, ct)
            : await db.MembershipPlans.FirstAsync(p => p.Code == PlanCode.Free, ct);

        return PlanLimits.Parse(plan.Limits);
    }
}
