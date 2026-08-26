using System.Text.Json;

namespace WellSense.Application.Common;

/// <summary>
/// Forma tipada de `membership_plans.limits` (jsonb, migración 017). `null` en
/// cualquiera de los dos campos significa "sin límite" — es el valor real que se sembró
/// para PROFESSIONAL, no un sentinel mágico como -1 o int.MaxValue.
/// </summary>
public record PlanLimits(int? MaxDevices, int? HistoryDays)
{
    public static readonly PlanLimits Unlimited = new(null, null);

    /// <summary>Nunca lanza — un `limits` vacío/malformado cae a "sin límite" (fail-open), nunca bloquea a alguien por un dato corrupto.</summary>
    public static PlanLimits Parse(string limitsJson)
    {
        if (string.IsNullOrWhiteSpace(limitsJson)) return Unlimited;
        try
        {
            var parsed = JsonSerializer.Deserialize<PlanLimits>(limitsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return parsed ?? Unlimited;
        }
        catch (JsonException)
        {
            return Unlimited;
        }
    }
}
