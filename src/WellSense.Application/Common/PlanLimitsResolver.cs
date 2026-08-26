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
///
/// Corregido tras la primera ronda de `dotnet test` real: la primera versión usaba
/// `FirstAsync` para buscar el plan FREE, que LANZA si no hay ningún `MembershipPlan`
/// sembrado — rompió 9 pruebas ya existentes (Devices/Wellness) que nunca tuvieron
/// razón de sembrar `MembershipPlans` antes de este bloque. Más grave que las pruebas
/// en sí: en un despliegue real, si el seed de planes faltara o una migración corriera
/// fuera de orden, CUALQUIER usuario sin suscripción de pago habría recibido un 500 al
/// registrar un dispositivo o ver su historial. Corregido a `FirstOrDefaultAsync` +
/// fallback a `PlanLimits.Unlimited` — fail-open, mismo criterio que
/// `PlanLimits.Parse` ya aplica ante un jsonb malformado. Nunca debe tumbar al
/// llamador por un catálogo de planes ausente o incompleto.
/// </summary>
public static class PlanLimitsResolver
{
    public static async Task<PlanLimits> ResolveForUserAsync(this IWellSenseDbContext db, Guid userId, CancellationToken ct)
    {
        var activeSubscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, ct);

        var plan = activeSubscription is not null
            ? await db.MembershipPlans.FirstOrDefaultAsync(p => p.Id == activeSubscription.PlanId, ct)
            : await db.MembershipPlans.FirstOrDefaultAsync(p => p.Code == PlanCode.Free, ct);

        return plan is not null ? PlanLimits.Parse(plan.Limits) : PlanLimits.Unlimited;
    }
}
