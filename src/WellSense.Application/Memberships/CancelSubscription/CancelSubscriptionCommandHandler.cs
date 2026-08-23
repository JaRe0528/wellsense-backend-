using MediatR;
using WellSense.Application.Memberships.SubscribeToPlan;

namespace WellSense.Application.Memberships.CancelSubscription;

/// <summary>
/// "Cancelar" = volver al plan FREE — no existe un estado "sin ninguna suscripción
/// activa" en el modelo de este backend (ver GetMyMembership: todo usuario siempre
/// tiene exactamente una). Reutiliza SubscribeToPlanCommand vía MediatR en vez de
/// duplicar la lógica de "cancelar la anterior + crear la nueva" — FREE nunca llama al
/// gateway de pago, así que IdempotencyKey aquí no importa funcionalmente (se genera una
/// nueva en cada llamado), pero el parámetro sigue siendo obligatorio en la forma del
/// comando por consistencia con el resto del módulo.
/// </summary>
public class CancelSubscriptionCommandHandler(ISender mediator) : IRequestHandler<CancelSubscriptionCommand, Unit>
{
    public async Task<Unit> Handle(CancelSubscriptionCommand request, CancellationToken ct)
    {
        await mediator.Send(new SubscribeToPlanCommand(request.CurrentUserId, "FREE", null, Guid.NewGuid().ToString()), ct);
        return Unit.Value;
    }
}
