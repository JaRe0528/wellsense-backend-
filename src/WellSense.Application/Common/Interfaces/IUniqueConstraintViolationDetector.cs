namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Abstrae la detección de "violación de índice único con nombre X" para que Application
/// no necesite referenciar Npgsql directamente (mantiene Domain/Application libres de
/// dependencias de infraestructura, según la regla de dependencia de Clean Architecture).
/// La implementación real vive en WellSense.Infrastructure e inspecciona la
/// PostgresException subyacente.
/// </summary>
public interface IUniqueConstraintViolationDetector
{
    bool IsUniqueViolation(Exception ex, string constraintName);
}
