using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Profiles.Goals.AddGoal;
using WellSense.Application.Profiles.Goals.DeleteGoal;
using WellSense.Application.Profiles.Goals.ListMyGoals;
using WellSense.Application.Profiles.GetMyProfile;
using WellSense.Application.Profiles.Onboarding.GetMySurvey;
using WellSense.Application.Profiles.Onboarding.UpsertMySurvey;
using WellSense.Application.Profiles.UpsertMyProfile;

namespace WellSense.Api.Controllers;

[ApiController]
[Route("api/v1/profiles/me")]
[Authorize]
public class ProfilesController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Get-or-create perezoso — ver HANDOFF: nunca 404, siempre devuelve un perfil (vacío si es la primera vez).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileResponse>> Get(CancellationToken ct)
    {
        var r = await mediator.Send(new GetMyProfileQuery(currentUser.UserId!.Value), ct);
        return Ok(new ProfileResponse(
            r.Id, r.FirstName, r.LastName, r.BirthDate, r.WeightKg, r.HeightCm,
            r.Occupation, r.AvatarUrl, r.Timezone, r.CreatedAt, r.UpdatedAt));
    }

    /// <summary>Upsert completo (PUT, no PATCH). `timezone` debe ser un identificador IANA válido.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert(UpsertProfileRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpsertMyProfileCommand(
            currentUser.UserId!.Value, request.FirstName, request.LastName, request.BirthDate,
            request.WeightKg, request.HeightCm, request.Occupation, request.AvatarUrl, request.Timezone), ct);
        return NoContent();
    }

    [HttpGet("goals")]
    [ProducesResponseType(typeof(IReadOnlyList<GoalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GoalResponse>>> ListGoals(CancellationToken ct)
    {
        var goals = await mediator.Send(new ListMyGoalsQuery(currentUser.UserId!.Value), ct);
        return Ok(goals.Select(g => new GoalResponse(g.Id, g.Type, g.TargetValue, g.CreatedAt)));
    }

    [HttpPost("goals")]
    [ProducesResponseType(typeof(AddGoalResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AddGoalResponse>> AddGoal(AddGoalRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new AddGoalCommand(currentUser.UserId!.Value, request.Type, request.TargetValue), ct);
        return CreatedAtAction(nameof(ListGoals), new AddGoalResponse(id));
    }

    [HttpDelete("goals/{goalId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGoal(Guid goalId, CancellationToken ct)
    {
        await mediator.Send(new DeleteGoalCommand(currentUser.UserId!.Value, goalId), ct);
        return NoContent();
    }

    /// <summary>204 (no 404) si el usuario todavía no contestó la encuesta — ver HANDOFF.</summary>
    [HttpGet("onboarding-survey")]
    [ProducesResponseType(typeof(OnboardingSurveyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<OnboardingSurveyResponse>> GetOnboardingSurvey(CancellationToken ct)
    {
        var r = await mediator.Send(new GetMyOnboardingSurveyQuery(currentUser.UserId!.Value), ct);
        if (r is null) return NoContent();
        return Ok(new OnboardingSurveyResponse(
            r.UsualSchedule, r.SleepSchedule, r.DeclaredActivityLevel, r.DeclaredStressLevel, r.DeclaredSleepQuality, r.CreatedAt));
    }

    /// <summary>Upsert — se puede recontestar la encuesta (ver decisión en el HANDOFF).</summary>
    [HttpPut("onboarding-survey")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertOnboardingSurvey(UpsertOnboardingSurveyRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpsertMyOnboardingSurveyCommand(
            currentUser.UserId!.Value, request.UsualSchedule, request.SleepSchedule,
            request.DeclaredActivityLevel, request.DeclaredStressLevel, request.DeclaredSleepQuality), ct);
        return NoContent();
    }
}
