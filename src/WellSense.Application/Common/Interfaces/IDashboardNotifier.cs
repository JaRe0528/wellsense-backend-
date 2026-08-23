namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Empuja eventos en vivo al dashboard web de un usuario (SignalR). La implementación
/// real vive en WellSense.Api (donde vive el Hub) — Application no depende de
/// Microsoft.AspNetCore.SignalR directamente, mismo principio que con IEmailSender/
/// ICurrentUserService: el "cómo" del transporte es un detalle de infraestructura/API,
/// Application solo declara "qué" necesita poder pasar.
/// </summary>
public interface IDashboardNotifier
{
    /// <summary>
    /// `eventType` es un identificador de evento estable (ej. "measurements_synced")
    /// para que el cliente decida qué volver a pedir — este canal es deliberadamente
    /// un canal de "algo cambió, refresca" y no transporta el payload completo del
    /// dashboard (ese cálculo es del bloque de ML/Dashboard, no de este).
    /// </summary>
    Task NotifyUserAsync(Guid userId, string eventType, object payload, CancellationToken ct = default);
}
