using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Billing;

namespace WellSense.Application.Memberships.SubscribeToPlan;

/// <summary>
/// Decisión de este bloque: una suscripción paga vive un mes desde que se activa
/// (`EndsAt = StartedAt.AddMonths(1)`) — es el "fin del período de facturación actual".
/// Este bloque NO implementa el job que renueva automáticamente o degrada a FREE cuando
/// `EndsAt` ya pasó; eso es candidato natural para un futuro bloque de jobs programados,
/// mismo tipo de alcance que la decisión de zona horaria del Bloque 3 (se documenta la
/// decisión y se deja el dato listo, sin construir el job que todavía nadie pidió). Las
/// suscripciones FREE no expiran por sí solas (`EndsAt = null`).
///
/// Orden de escritura deliberadamente NO atómico en un único SaveChanges cuando hay que
/// reemplazar una suscripción activa existente: `ux_subscriptions_one_active_per_user`
/// es un índice único NO diferible (se evalúa por sentencia, no al final de la
/// transacción) — no hay garantía documentada de que EF Core emita el UPDATE de la fila
/// vieja (Active→Canceled) ANTES que el INSERT de la fila nueva (Active) dentro de una
/// misma llamada a SaveChanges cuando son dos filas del mismo tipo sin relación de FK
/// entre sí. Apostar a ese orden implícito podría violar el índice de forma intermitente
/// y muy difícil de reproducir — se prefiere partir en dos llamadas secuenciales
/// explícitas: primero se confirma que la vieja ya no está activa, después se crea la
/// nueva. El costo aceptado: un crash exactamente entre esas dos llamadas dejaría al
/// usuario sin suscripción activa un instante — se autorrepara solo en el próximo
/// GetMyMembership (lazy-crea FREE), aunque eso signifique perder momentáneamente el
/// plan pago hasta que se reintente la suscripción. Ver riesgo en el HANDOFF.
///
/// El cobro (`ChargeAsync`) se llama EXACTAMENTE UNA VEZ, antes de tocar la BD —
/// deliberado: cobrar dos veces por accidente en un handler de dinero real es el tipo de
/// bug que no se debe arriesgar ni una vez, así que el resultado del cobro se guarda en
/// una variable y se reutiliza, nunca se vuelve a invocar el gateway más abajo.
///
/// Modificado en Bloque 10 (auditoría completa): se agregó un registro en `audit_logs`
/// (`subscription_changed`) en el Paso 2, junto con la nueva suscripción — cubre tanto
/// planes pagos como FREE (incluido el camino de "cancelar" vía CancelSubscriptionCommandHandler,
/// que reusa este mismo handler). Nunca se registra en el camino declinado — ese intento
/// ya queda registrado en `payments` con status DECLINED, no hace falta duplicarlo aquí.
/// </summary>
public class SubscribeToPlanCommandHandler(
    IWellSenseDbContext db,
    IPaymentGateway paymentGateway,
    IDateTimeProvider clock) : IRequestHandler<SubscribeToPlanCommand, SubscribeToPlanResult>
{
    public async Task<SubscribeToPlanResult> Handle(SubscribeToPlanCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<PlanCode>(request.PlanCode, ignoreCase: true, out var planCode))
            throw PaymentDomainException.PlanNotFound();

        var plan = await db.MembershipPlans.FirstOrDefaultAsync(p => p.Code == planCode, ct)
            ?? throw PaymentDomainException.PlanNotFound();

        ChargeResult? charge = null;

        if (plan.PriceCents > 0)
        {
            if (string.IsNullOrWhiteSpace(request.PaymentMethodToken))
                throw PaymentDomainException.PaymentMethodRequired();

            charge = await paymentGateway.ChargeAsync(
                request.PaymentMethodToken, plan.PriceCents, plan.Currency, request.IdempotencyKey, ct);

            if (!charge.Approved)
            {
                // El registro del intento fallido se persiste igual (auditoría/historial
                // de pagos) — solo que nunca queda ligado a ninguna suscripción
                // (subscription_id NULL, exigido por el CHECK de la BD para status
                // DECLINED). Este paso nunca toca la suscripción activa del usuario.
                db.Payments.Add(new Payment
                {
                    Id = Guid.NewGuid(),
                    UserId = request.CurrentUserId,
                    PlanId = plan.Id,
                    SubscriptionId = null,
                    AmountCents = plan.PriceCents,
                    Currency = plan.Currency,
                    Status = PaymentStatus.Declined,
                    CardBrand = charge.CardBrand,
                    CardLast4 = charge.CardLast4,
                    TransactionId = charge.TransactionId,
                    CreatedAt = clock.UtcNow
                });
                await db.SaveChangesAsync(ct);

                throw PaymentDomainException.Declined(charge.DeclineReason ?? "motivo no especificado por la pasarela");
            }
        }

        // Paso 1 (si aplica): confirmar que la suscripción activa anterior, si existía,
        // ya no lo está — SOLA, antes de crear la nueva. Ver comentario de la clase.
        var previousActive = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == request.CurrentUserId && s.Status == SubscriptionStatus.Active, ct);
        if (previousActive is not null)
        {
            previousActive.Status = SubscriptionStatus.Canceled;
            previousActive.EndsAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        // Paso 2: la nueva suscripción activa, y si hubo cobro APROBADO (ya resuelto
        // arriba, no se vuelve a llamar al gateway aquí), el pago ligado a ella — estos
        // dos sí viajan juntos en una sola llamada: hay una FK real Payment→Subscription,
        // y para esa relación EF Core sí garantiza el orden Subscription-antes-que-Payment.
        var newSubscription = new Subscription
        {
            Id = Guid.NewGuid(),
            UserId = request.CurrentUserId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            StartedAt = clock.UtcNow,
            EndsAt = plan.PriceCents > 0 ? clock.UtcNow.AddMonths(1) : null
        };
        db.Subscriptions.Add(newSubscription);

        Guid? paymentId = null;
        if (charge is not null) // plan pago y ya aprobado (el camino declinado ya retornó/lanzó arriba)
        {
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = request.CurrentUserId,
                PlanId = plan.Id,
                SubscriptionId = newSubscription.Id,
                AmountCents = plan.PriceCents,
                Currency = plan.Currency,
                Status = PaymentStatus.Approved,
                CardBrand = charge.CardBrand,
                CardLast4 = charge.CardLast4,
                TransactionId = charge.TransactionId,
                CreatedAt = clock.UtcNow
            };
            db.Payments.Add(payment);
            paymentId = payment.Id;
        }

        db.AuditLogs.Add(new WellSense.Domain.Identity.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.CurrentUserId,
            Action = "subscription_changed",
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                subscriptionId = newSubscription.Id,
                planCode = plan.Code.ToString().ToUpperInvariant(),
                paid = charge is not null
            }),
            CreatedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);

        return new SubscribeToPlanResult(
            newSubscription.Id, plan.Code.ToString().ToUpperInvariant(), newSubscription.Status.ToString().ToUpperInvariant(),
            newSubscription.StartedAt, newSubscription.EndsAt, paymentId);
    }
}
