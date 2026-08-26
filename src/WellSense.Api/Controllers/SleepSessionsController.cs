using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.SleepSessions.ListMySleepSessions;

namespace WellSense.Api.Controllers;

/// <summary>
/// Fix urgente: Web asumía GET /sync?type=sleep, que nunca existió — SyncController
/// (Bloque 4) solo tiene POST /sync/measurements. Este es un endpoint de lectura nuevo,
/// no una modificación de Sync.
/// </summary>
[ApiController]
[Route("api/v1/sleep-sessions")]
[Authorize]
public class SleepSessionsController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Sesiones de sueño del usuario en los últimos `days` días (default 30), más reciente primero.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SleepSessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SleepSessionResponse>>> ListMine([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var sessions = await mediator.Send(new ListMySleepSessionsQuery(currentUser.UserId!.Value, days), ct);
        return Ok(sessions.Select(s => new SleepSessionResponse(s.Id, s.StartAt, s.EndAt, s.DurationMinutes, s.Stages, s.CreatedAt)));
    }
}
