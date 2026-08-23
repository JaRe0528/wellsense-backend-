using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Memberships.ListPlans;

/// <summary>
/// Público (sin Bearer) — ver AllowAnonymous en el controller. El catálogo de planes es
/// contenido de una página de precios normal, no información de la cuenta de nadie.
/// </summary>
public class ListPlansQueryHandler(IWellSenseDbContext db) : IRequestHandler<ListPlansQuery, IReadOnlyList<PlanResult>>
{
    public async Task<IReadOnlyList<PlanResult>> Handle(ListPlansQuery request, CancellationToken ct)
    {
        var plans = await db.MembershipPlans
            .OrderBy(p => p.PriceCents)
            .ToListAsync(ct);

        // .ToString().ToUpperInvariant() en memoria, no dentro del Select traducido a SQL
        // — mismo motivo que ListMyDevicesQueryHandler (Bloque 4): Code usa un
        // HasConversion con lambdas propias, y EF Core no siempre puede traducir
        // Enum.ToString() sobre una conversión personalizada dentro de una query SQL.
        return plans
            .Select(p => new PlanResult(p.Id, p.Code.ToString().ToUpperInvariant(), p.Name, p.PriceCents, p.Currency))
            .ToList();
    }
}
