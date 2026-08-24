using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Wellness.ComputeDailyScores;
using WellSense.Application.Wellness.GetMyDailyScores;
using WellSense.Application.Wellness.GetMyScoreHistory;

namespace WellSense.Api.Controllers;

/// <summary>
/// ML V1 (reglas) — ver HANDOFF de este bloque para el detalle completo del motor de
/// puntuación. El "día" siempre se calcula en la zona horaria local del usuario
/// (profiles.timezone), nunca en UTC — decisión del Bloque 3, aplicada acá.
/// </summary>
[ApiController]
[Route("api/v1/wellness")]
[Authorize]
public class WellnessController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// Calcula (o recalcula) los puntajes de un día a partir de los measurements/sleep/
    /// activity ya sincronizados. Sin `date`, calcula "hoy" en la zona horaria del
    /// usuario. Recalculable: si ese día ya tenía puntaje, se actualiza.
    /// </summary>
    [HttpPost("compute")]
    [ProducesResponseType(typeof(ComputeScoresResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ComputeScoresResponse>> Compute(ComputeScoresRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ComputeDailyScoresCommand(currentUser.UserId!.Value, request.Date), ct);

        return Ok(new ComputeScoresResponse(
            result.Date,
            result.Wellness is null ? null : new WellnessScoreResponse(result.Wellness.Score),
            result.Stress is null ? null : new StressScoreResponse(result.Stress.Score, result.Stress.Level, result.Stress.Confidence)));
    }

    /// <summary>Puntajes de un día puntual. Sin `date`, usa "hoy" en la zona horaria del usuario. Nunca 404 — campos null si ese día no tiene puntaje calculado todavía.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(DailyScoresResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DailyScoresResponse>> GetMine([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyDailyScoresQuery(currentUser.UserId!.Value, date), ct);

        return Ok(new DailyScoresResponse(
            result.Date, result.WellnessScore, result.StressScore, result.StressLevel, result.StressConfidence));
    }

    /// <summary>Historial para graficar — últimos `days` días (default 7, tope 90) terminando en "hoy" según la zona horaria del usuario.</summary>
    [HttpGet("me/history")]
    [ProducesResponseType(typeof(IReadOnlyList<DailyScoreHistoryItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DailyScoreHistoryItemResponse>>> GetHistory([FromQuery] int days, CancellationToken ct)
    {
        var boundedDays = Math.Clamp(days <= 0 ? 7 : days, 1, 90);
        var result = await mediator.Send(new GetMyScoreHistoryQuery(currentUser.UserId!.Value, boundedDays), ct);

        return Ok(result.Select(r => new DailyScoreHistoryItemResponse(r.Date, r.WellnessScore, r.StressScore, r.StressLevel)));
    }
}
