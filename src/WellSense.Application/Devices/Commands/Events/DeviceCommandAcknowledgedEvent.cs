using MediatR;

namespace WellSense.Application.Devices.Commands.Events;

/// <summary>
/// Evento de integración (no HTTP) — se publica DESPUÉS de que un ACK ya se confirmó en
/// BD, para avisarle al dashboard web en vivo (mismo patrón que MeasurementsSyncedEvent,
/// Bloque 5). Cierra el loop: Web emite el comando por REST → Android lo recibe por
/// SignalR → Android confirma por REST → Web se entera del resultado por SignalR, sin
/// tener que hacer polling.
/// </summary>
public record DeviceCommandAcknowledgedEvent(Guid UserId, Guid DeviceId, Guid CommandId, string Status) : INotification;
