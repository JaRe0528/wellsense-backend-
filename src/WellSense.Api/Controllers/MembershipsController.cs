using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Memberships.CancelSubscription;
using WellSense.Application.Memberships.GetMyMembership;
using WellSense.Application.Memberships.ListPlans;
using WellSense.Application.Memberships.SubscribeToPlan;

namespace WellSense.Api.Controllers;

[ApiController]
[Route("api/v1/memberships")]
public class MembershipsController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Público — catálogo de precios, no requiere sesión. Ver HANDOFF.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanResponse>>> ListPlans(CancellationToken ct)
    {
        var plans = await mediator.Send(new ListPlansQuery(), ct);
        return Ok(plans.Select(p => new PlanResponse(p.Id, p.Code, p.Name, p.PriceCents, p.Currency)));
    }

    /// <summary>Get-or-create perezoso — nunca 404, todo usuario tiene siempre una membresía (FREE si nunca contrató nada).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MembershipResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MembershipResponse>> GetMyMembership(CancellationToken ct)
    {
        var m = await mediator.Send(new GetMyMembershipQuery(currentUser.UserId!.Value), ct);
        return Ok(new MembershipResponse(m.SubscriptionId, m.PlanCode, m.PlanName, m.Status, m.StartedAt, m.EndsAt));
    }

    /// <summary>
    /// Cambia de plan (incluye contratar por primera vez). Para planes pagos,
    /// `paymentMethodToken` es obligatorio — nunca mandar datos de tarjeta en claro, solo
    /// el token que ya tokenizó el SDK de Stripe en el cliente. `idempotencyKey` evita
    /// cobros duplicados en reintentos de red — mismo patrón que `requestId` en /sync.
    /// </summary>
    [HttpPost("subscribe")]
    [Authorize]
    [ProducesResponseType(typeof(SubscribeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SubscribeResponse>> Subscribe(SubscribeRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new SubscribeToPlanCommand(
            currentUser.UserId!.Value, request.PlanCode, request.PaymentMethodToken, request.IdempotencyKey), ct);

        return Ok(new SubscribeResponse(
            result.SubscriptionId, result.PlanCode, result.Status, result.StartedAt, result.EndsAt, result.PaymentId));
    }

    /// <summary>Sugar de `subscribe` a FREE — ver HANDOFF sobre por qué "cancelar" siempre significa "volver a FREE".</summary>
    [HttpPost("cancel")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        await mediator.Send(new CancelSubscriptionCommand(currentUser.UserId!.Value), ct);
        return NoContent();
    }
}
