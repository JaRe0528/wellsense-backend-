namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Empuja un comando en vivo al dispositivo (Android) por SignalR — mismo principio que
/// IDashboardNotifier (Bloque 5): Application no depende de Microsoft.AspNetCore.SignalR
/// directamente, la implementación real (con el Hub) vive en Api.
///
/// A diferencia de IDashboardNotifier (que empuja hacia el grupo de un USUARIO), este
/// empuja hacia el grupo de un DISPOSITIVO puntual — un usuario puede tener varios
/// dispositivos, y un comando siempre va dirigido a uno específico (ej. "inicia el
/// monitoreo EN ESTE reloj", no en todos los dispositivos del usuario).
/// </summary>
public interface IDeviceCommandNotifier
{
    Task NotifyDeviceAsync(Guid deviceId, object command, CancellationToken ct = default);
}
