using MediatR;

namespace WellSense.Application.SleepSessions.ListMySleepSessions;

public record ListMySleepSessionsQuery(Guid CurrentUserId, int Days) : IRequest<IReadOnlyList<SleepSessionResult>>;

/// <summary>
/// `Stages` viaja como el string jsonb crudo (nunca deserializado a un objeto anidado
/// aquí) — mismo criterio que `DeviceCommandResponse.Payload` (Bloque 8): consistente
/// con cómo el resto de la API ya expone columnas jsonb, en vez de introducir un patrón
/// nuevo solo para este endpoint. Web hace `JSON.parse(stages)` si necesita inspeccionar
/// el objeto.
/// </summary>
public record SleepSessionResult(
    Guid Id, DateTimeOffset StartAt, DateTimeOffset EndAt, int DurationMinutes, string Stages, DateTimeOffset CreatedAt);
