namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Cobro real contra una pasarela de pago. El backend NUNCA recibe ni toca datos de
/// tarjeta en claro (número, CVV) — el cliente (Web/Android) tokeniza la tarjeta con el
/// SDK de la pasarela (ej. Stripe.js/Stripe SDK) y solo nos manda el token resultante.
/// Esto es requisito de PCI-DSS, no una preferencia de diseño.
///
/// `amountCents`/`currency` SIEMPRE los decide el servidor a partir del plan elegido
/// (`membership_plans.price_cents`/`currency`) — nunca se aceptan del cliente. Aceptar un
/// monto del cliente permitiría a alguien pagar $1 por un plan de $399 con un cliente
/// modificado.
/// </summary>
public interface IPaymentGateway
{
    Task<ChargeResult> ChargeAsync(
        string paymentMethodToken, int amountCents, string currency, string idempotencyKey, CancellationToken ct = default);
}

/// <summary>
/// `TransactionId` es el identificador que asigna la PASARELA (ej. el id del PaymentIntent
/// de Stripe) — nunca uno generado por nosotros; es lo que persiste en
/// `payments.transaction_id` y lo que se usaría para conciliar con el estado de cuenta del
/// proveedor. `CardBrand`/`CardLast4` vienen del cobro ya procesado, no del cliente.
/// </summary>
public record ChargeResult(bool Approved, string TransactionId, string? CardBrand, string? CardLast4, string? DeclineReason);
