using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.ActivitySessions.ListMyActivitySessions;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Api.Controllers;

[ApiController]
[Route("api/v1/activity-sessions")]
[Authorize]
public class ActivitySessionsController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Sesiones de actividad del usuario en los últimos `days` días (default 30), más reciente primero.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ActivitySessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ActivitySessionResponse>>> ListMine([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var sessions = await mediator.Send(new ListMyActivitySessionsQuery(currentUser.UserId!.Value, days), ct);
        return Ok(sessions.Select(a => new ActivitySessionResponse(a.Id, a.Type, a.StartAt, a.EndAt, a.Steps, a.DistanceM, a.Calories, a.CreatedAt)));
    }
}
