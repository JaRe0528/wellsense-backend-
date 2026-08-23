using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Payments.ListMyPayments;

public class ListMyPaymentsQueryHandler(IWellSenseDbContext db)
    : IRequestHandler<ListMyPaymentsQuery, IReadOnlyList<PaymentResult>>
{
    public async Task<IReadOnlyList<PaymentResult>> Handle(ListMyPaymentsQuery request, CancellationToken ct)
    {
        // Se trae el Payment completo (con Include del plan) y se traduce el enum a
        // string DESPUÉS, en memoria — mismo motivo documentado en ListPlansQueryHandler/
        // ListMyDevicesQueryHandler: Status/Code usan HasConversion con lambdas propias,
        // riesgoso de traducir dentro de un Select a SQL.
        var payments = await db.Payments
            .Where(p => p.UserId == request.CurrentUserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        if (payments.Count == 0) return [];

        var planIds = payments.Select(p => p.PlanId).Distinct().ToList();
        var plans = await db.MembershipPlans
            .Where(p => planIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        return payments
            .Select(p => new PaymentResult(
                p.Id,
                plans[p.PlanId].Code.ToString().ToUpperInvariant(),
                p.AmountCents,
                p.Currency,
                p.Status.ToString().ToUpperInvariant(),
                p.CardBrand,
                p.CardLast4,
                p.CreatedAt))
            .ToList();
    }
}
