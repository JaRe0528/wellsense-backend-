using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Memberships.ListPlans;

/// <summary>
/// Público (sin Bearer) — ver AllowAnonymous en el controller. El catálogo de planes es
/// contenido de una página de precios normal, no información de la cuenta de nadie.
///
/// Modificado post-Bloque-10 (Partes 3+4): ahora expone `features` (arreglo de strings,
/// migración 017) y `limits` (los mismos límites reales que YA se aplican en
/// RegisterDeviceCommandHandler/GetMyScoreHistoryQueryHandler vía PlanLimits.Parse — no
/// una copia separada, la misma fuente de verdad) para que Web pueda mostrarlos igual
/// que el precio.
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
            .Select(p =>
            {
                var limits = PlanLimits.Parse(p.Limits);
                var features = ParseFeatures(p.Features);
                return new PlanResult(
                    p.Id, p.Code.ToString().ToUpperInvariant(), p.Name, p.PriceCents, p.Currency,
                    features, new PlanLimitsResult(limits.MaxDevices, limits.HistoryDays));
            })
            .ToList();
    }

    private static IReadOnlyList<string> ParseFeatures(string featuresJson)
    {
        if (string.IsNullOrWhiteSpace(featuresJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(featuresJson) ?? [];
        }
        catch (JsonException)
        {
            return []; // nunca tumbar el catálogo por un jsonb malformado
        }
    }
}
