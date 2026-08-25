namespace WellSense.Application.Common.Exceptions;

/// <summary>
/// Excepción de negocio para el módulo Admin (Bloque 9). Misma forma que las demás
/// excepciones de dominio (ErrorCode estable + HttpStatus), clase separada por el mismo
/// motivo de siempre: no arriesgar pruebas ya aprobadas de otros módulos.
/// </summary>
public class AdminDomainException(string errorCode, string message, int httpStatus = 400) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public int HttpStatus { get; } = httpStatus;

    public static AdminDomainException UserNotFound()
        => new("USER_NOT_FOUND", "El usuario no existe.", 404);

    public static AdminDomainException CannotSuspendSelf()
        => new("CANNOT_SUSPEND_SELF", "Un admin no puede suspender su propia cuenta.", 400);

    public static AdminDomainException AlreadyBootstrapped()
        => new("ALREADY_BOOTSTRAPPED", "Ya existe al menos un administrador — este endpoint solo funciona una vez.", 409);

    public static AdminDomainException InvalidBootstrapSecret()
        => new("INVALID_BOOTSTRAP_SECRET", "Secreto de bootstrap inválido.", 403);
}
