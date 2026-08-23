namespace WellSense.Application.Common.Exceptions;

/// <summary>
/// Excepción de negocio para el flujo de Auth, con un código de error estable que el
/// cliente (Web/Android) puede usar para decidir el mensaje/UX exacto, en vez de
/// parsear el texto del mensaje. El middleware global la traduce a un ProblemDetails
/// con el status HTTP indicado aquí.
/// </summary>
public class AuthDomainException(string errorCode, string message, int httpStatus = 400) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public int HttpStatus { get; } = httpStatus;

    public static AuthDomainException InvalidCredentials()
        => new("INVALID_CREDENTIALS", "Email o contraseña incorrectos.", 401);

    public static AuthDomainException EmailNotVerified()
        => new("EMAIL_NOT_VERIFIED", "Debes verificar tu correo antes de iniciar sesión.", 403);

    public static AuthDomainException AccountNotActive()
        => new("ACCOUNT_NOT_ACTIVE", "Esta cuenta no está activa.", 403);

    /// <summary>
    /// El JWT es válido (firma/expiración correctas) pero el usuario que representa ya
    /// no existe o fue borrado (soft-delete) — caso borde: token emitido antes de un
    /// DeleteMe, todavía dentro de su ventana de 15 min de vida. 401, no 404: desde la
    /// perspectiva del cliente, su sesión ya no es válida, no que "el recurso no existe".
    /// </summary>
    public static AuthDomainException AccountNotFound()
        => new("ACCOUNT_NOT_FOUND", "Esta cuenta ya no existe.", 401);

    public static AuthDomainException EmailAlreadyRegistered()
        => new("EMAIL_ALREADY_REGISTERED", "Ya existe una cuenta con este correo.", 409);

    public static AuthDomainException InvalidOrExpiredToken()
        => new("INVALID_OR_EXPIRED_TOKEN", "El token es inválido o ya expiró.", 400);

    public static AuthDomainException InvalidOrReusedRefreshToken()
        => new("INVALID_REFRESH_TOKEN", "La sesión no es válida. Inicia sesión de nuevo.", 401);

    public static AuthDomainException InvalidDeviceLinkCode()
        => new("INVALID_DEVICE_LINK_CODE", "El código es inválido o ya expiró.", 400);

    public static AuthDomainException DeviceLinkCodeLocked()
        => new("DEVICE_LINK_CODE_LOCKED", "Se superó el número de intentos permitidos para este código.", 429);
}
