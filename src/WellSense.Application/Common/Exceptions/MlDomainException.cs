namespace WellSense.Application.Common.Exceptions;

/// <summary>
/// Excepción de negocio para ML/Wellness (Bloque 7). Misma forma que las excepciones de
/// los demás módulos (ErrorCode estable + HttpStatus) — clase separada por el mismo
/// motivo de siempre: no arriesgar las pruebas ya aprobadas de bloques anteriores.
/// </summary>
public class MlDomainException(string errorCode, string message, int httpStatus = 400) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public int HttpStatus { get; } = httpStatus;

    /// <summary>Ni sueño/actividad (wellness) ni frecuencia cardíaca/sueño (stress) tienen ningún dato ese día.</summary>
    public static MlDomainException InsufficientData()
        => new("INSUFFICIENT_DATA", "No hay suficientes datos sincronizados para calcular los puntajes de ese día.", 400);
}
