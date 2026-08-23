using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Payments;

/// <summary>
/// Implementación real contra Stripe (SDK oficial `Stripe.net`) — no un stub, mismo
/// criterio que FirebaseCloudMessagingSender (Bloque 5): la Web necesita algo que
/// funcione en cuanto DevSecOps coloque `Stripe:SecretKey`.
///
/// Elegí Stripe sobre otras pasarelas (Conekta, Mercado Pago) porque no había ninguna
/// decidida en 01-ARQUITECTURA-Y-STACK.md — es una decisión nueva de este bloque, no del
/// documento maestro original, y la dejo explícita para que el chat de
/// arquitectura/orquestador la confirme o la cambie si ya tenían otra en mente. Soporta
/// MXN nativamente y su modelo de tokenización (Stripe.js en el cliente, el backend
/// nunca toca el número de tarjeta) es exactamente lo que exige IPaymentGateway.
///
/// A diferencia de FCM, si `Stripe:SecretKey` falta esto SÍ lanza (PaymentDomainException,
/// 503) en vez de fallar en silencio — un cobro nunca debe fingir éxito ni fallar callado,
/// ver la propia excepción.
/// </summary>
public class StripePaymentGateway(IConfiguration configuration, ILogger<StripePaymentGateway> logger) : IPaymentGateway
{
    private static readonly object InitLock = new();
    private static bool _configured;

    public async Task<ChargeResult> ChargeAsync(
        string paymentMethodToken, int amountCents, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        EnsureConfigured();

        var options = new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = currency.ToLowerInvariant(),
            PaymentMethod = paymentMethodToken,
            PaymentMethodTypes = ["card"],
            Confirm = true,
            // Sin este Expand, PaymentIntent.PaymentMethod solo trae el id como
            // referencia — hace falta pedirlo expandido para leer marca/últimos 4
            // dígitos de la tarjeta que efectivamente se cobró (nunca se le piden al
            // cliente, siempre vienen de la respuesta de Stripe).
            Expand = ["payment_method"]
        };
        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };

        try
        {
            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(options, requestOptions, ct);

            var approved = intent.Status == "succeeded";
            var card = intent.PaymentMethod?.Card;

            return new ChargeResult(
                approved,
                intent.Id,
                card?.Brand,
                card?.Last4,
                approved ? null : intent.LastPaymentError?.Message ?? "El pago no se completó.");
        }
        catch (StripeException ex)
        {
            // Una tarjeta rechazada NO es una excepción de nuestro código — es un
            // resultado de negocio válido (Approved=false), nunca debe tumbar el
            // handler que llamó. Solo un problema real de configuración/red se
            // propagaría más allá de este catch.
            logger.LogWarning(ex, "Cobro rechazado o fallido por Stripe.");
            return new ChargeResult(
                false,
                ex.StripeError?.PaymentIntent?.Id ?? $"stripe-error-{Guid.NewGuid()}",
                null, null,
                ex.StripeError?.Message ?? ex.Message);
        }
    }

    private void EnsureConfigured()
    {
        if (_configured) return;
        lock (InitLock)
        {
            if (_configured) return;

            var secretKey = configuration["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
                throw PaymentDomainException.GatewayNotConfigured();

            StripeConfiguration.ApiKey = secretKey;
            _configured = true;
        }
    }
}
