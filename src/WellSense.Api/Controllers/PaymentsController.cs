using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Payments.ListMyPayments;

namespace WellSense.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public class PaymentsController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Historial de pagos del usuario — incluye tanto aprobados como rechazados.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentResponse>>> ListMine(CancellationToken ct)
    {
        var payments = await mediator.Send(new ListMyPaymentsQuery(currentUser.UserId!.Value), ct);
        return Ok(payments.Select(p => new PaymentResponse(
            p.Id, p.PlanCode, p.AmountCents, p.Currency, p.Status, p.CardBrand, p.CardLast4, p.CreatedAt)));
    }
}
