using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Users.DeleteMe;
using WellSense.Application.Users.GetMe;

namespace WellSense.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> GetMe(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMeQuery(currentUser.UserId!.Value), ct);
        return Ok(new MeResponse(result.Id, result.Email, result.EmailVerified, result.Role, result.Status, result.CreatedAt));
    }

    /// <summary>Soft-delete de la cuenta. Exige la contraseña actual — ver justificación en el HANDOFF.</summary>
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMe(DeleteMeRequest request, CancellationToken ct)
    {
        await mediator.Send(new DeleteMeCommand(currentUser.UserId!.Value, request.CurrentPassword), ct);
        return NoContent();
    }
}
