using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Sync.SyncMeasurements;

namespace WellSense.Api.Controllers;

[ApiController]
[Route("api/v1/sync")]
[Authorize]
public class SyncController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// Idempotente en dos niveles — ver HANDOFF de este bloque: `requestId` identifica el
    /// batch completo (reintentar la misma llamada nunca duplica trabajo), y `id` de cada
    /// medición identifica el evento individual (puede reaparecer en otro batch sin
    /// error, se cuenta como duplicado). Nunca falla el batch completo por una medición
    /// individual inválida — esas se listan en `rejectedItems`, el resto se procesa.
    /// </summary>
    [HttpPost("measurements")]
    [ProducesResponseType(typeof(SyncMeasurementsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SyncMeasurementsResponse>> SyncMeasurements(SyncMeasurementsRequest request, CancellationToken ct)
    {
        var command = new SyncMeasurementsCommand(
            currentUser.UserId!.Value,
            request.DeviceId,
            request.RequestId,
            request.Measurements.Select(m => new MeasurementItem(m.Id, m.Type, m.Value, m.Unit, m.RecordedAt)).ToList());

        var result = await mediator.Send(command, ct);

        return Ok(new SyncMeasurementsResponse(
            result.RequestId, result.Status, result.AcceptedCount, result.DuplicatedCount, result.RejectedCount,
            result.RejectedItems.Select(r => new RejectedItemResponse(r.Id, r.Reason)).ToList()));
    }
}
