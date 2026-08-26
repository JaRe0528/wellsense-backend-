namespace WellSense.Application.Common.Exceptions;

/// <summary>
/// Excepción de negocio para Memberships/Payments (Bloque 6). Misma forma que
/// AuthDomainException/SyncDomainException (ErrorCode estable + HttpStatus), y misma
/// razón para no reutilizar aquellas: no arriesgar las pruebas ya aprobadas de bloques
/// anteriores que verifican el tipo exacto de excepción.
/// </summary>
public class PaymentDomainException(string errorCode, string message, int httpStatus = 400) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public int HttpStatus { get; } = httpStatus;

    public static PaymentDomainException PlanNotFound()
        => new("PLAN_NOT_FOUND", "El plan solicitado no existe.", 404);

    public static PaymentDomainException PaymentMethodRequired()
        => new("PAYMENT_METHOD_REQUIRED", "Este plan requiere un método de pago.", 400);

    /// <summary>
    /// 402 Payment Required — el único lugar de todo el backend donde ese status
    /// realmente aplica de forma literal.
    /// </summary>
    public static PaymentDomainException Declined(string reason)
        => new("PAYMENT_DECLINED", $"El pago fue rechazado: {reason}", 402);

    /// <summary>
    /// La pasarela no tiene credenciales configuradas (Stripe:SecretKey ausente). A
    /// diferencia de FCM (Bloque 5), donde un push no configurado simplemente no se envía
    /// en silencio, un cobro no configurado NUNCA debe fingir éxito ni fallar en
    /// silencio — el cliente necesita un error explícito. 503, no 500: es la
    /// infraestructura de pagos la que no está lista, no un bug del código.
    /// </summary>
    public static PaymentDomainException GatewayNotConfigured()
        => new("PAYMENT_GATEWAY_NOT_CONFIGURED", "El sistema de pagos no está disponible en este momento.", 503);

    /// <summary>
    /// Parte 3 del encargo post-Bloque-10: límites reales por plan. 403, no 400 — no es
    /// que la request esté mal formada, es que el plan actual del usuario no autoriza la
    /// acción (mismo espíritu que un usuario normal contra un endpoint de Admin).
    /// </summary>
    public static PaymentDomainException PlanLimitExceeded(string limitName)
        => new("PLAN_LIMIT_EXCEEDED", $"Tu plan actual no permite más de {limitName}. Mejora tu plan para continuar.", 403);
}
