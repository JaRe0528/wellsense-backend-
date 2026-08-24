namespace WellSense.Application.Common.Exceptions;

/// <summary>
/// Excepción de negocio para Devices/Measurements/Sync (Bloque 4). Misma forma que
/// AuthDomainException (ErrorCode estable + HttpStatus), pero deliberadamente una clase
/// separada en vez de reutilizar AuthDomainException: esta última ya tiene pruebas
/// aprobadas en Bloques 2-3 que verifican el tipo exacto de excepción
/// (`.ThrowAsync&lt;AuthDomainException&gt;()`) — cambiarla a una jerarquía compartida
/// hubiera arriesgado romper ese código ya cerrado. Si este patrón de "una excepción de
/// dominio por módulo" se repite en más bloques, vale la pena unificarlas bajo una base
/// común en algún momento — no se hizo aquí para no tocar Bloque 2/3.
/// </summary>
public class SyncDomainException(string errorCode, string message, int httpStatus = 400) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public int HttpStatus { get; } = httpStatus;

    public static SyncDomainException DeviceNotFound()
        => new("DEVICE_NOT_FOUND", "El dispositivo no existe o no pertenece a este usuario.", 404);

    public static SyncDomainException BatchTooLarge(int max)
        => new("SYNC_BATCH_TOO_LARGE", $"Un batch de sincronización no puede tener más de {max} mediciones.", 400);

    /// <summary>Bloque 8 (Device Command System) — mismo criterio de reuso que DeviceNotFound: el ACK apunta a un comando de un dispositivo, sigue siendo el mismo dominio de Devices.</summary>
    public static SyncDomainException CommandNotFound()
        => new("COMMAND_NOT_FOUND", "El comando no existe o no pertenece a este dispositivo.", 404);
}
