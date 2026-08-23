using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Billing;

namespace WellSense.Application.Memberships.GetMyMembership;

/// <summary>
/// Get-or-create perezoso, mismo patrón que GetMyProfile (Bloque 3): todo usuario
/// "tiene" una membresía siempre — si nunca contrató nada, se le crea una suscripción
/// FREE en este mismo llamado, en vez de exigir un paso explícito de "activar tu cuenta
/// gratis" o devolver null/404. Así el invariante "el usuario siempre tiene EXACTAMENTE
/// una suscripción activa" se mantiene desde el primer GET, sin tener que tocar
/// RegisterCommandHandler (Bloque 2, ya cerrado).
/// </summary>
public class GetMyMembershipQueryHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<GetMyMembershipQuery, MembershipResult>
{
    public async Task<MembershipResult> Handle(GetMyMembershipQuery request, CancellationToken ct)
    {
        var subscription = await db.Subscriptions
            .Where(s => s.UserId == request.CurrentUserId && s.Status == SubscriptionStatus.Active)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
        {
            var freePlan = await db.MembershipPlans.FirstAsync(p => p.Code == PlanCode.Free, ct);
            subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                UserId = request.CurrentUserId,
                PlanId = freePlan.Id,
                Status = SubscriptionStatus.Active,
                StartedAt = clock.UtcNow
            };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync(ct);
        }

        var plan = await db.MembershipPlans.FirstAsync(p => p.Id == subscription.PlanId, ct);

        return new MembershipResult(
            subscription.Id, plan.Code.ToString().ToUpperInvariant(), plan.Name,
            subscription.Status.ToString().ToUpperInvariant(), subscription.StartedAt, subscription.EndsAt);
    }
}
