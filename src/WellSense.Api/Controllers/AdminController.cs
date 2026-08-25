using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Authorization;
using WellSense.Api.Contracts;
using WellSense.Application.Admin.BootstrapFirstAdmin;
using WellSense.Application.Admin.GetStats;
using WellSense.Application.Admin.GetUserDetail;
using WellSense.Application.Admin.ListActiveSubscriptions;
using WellSense.Application.Admin.ListAuditLogs;
using WellSense.Application.Admin.ListUsers;
using WellSense.Application.Admin.UpdateUserStatus;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Api.Controllers;

/// <summary>
/// Toda la superficie administrativa — ver HANDOFF de Bloque 9 para la justificación
/// completa de por qué esto siempre da 403 (nunca 404) a un usuario sin el rol Admin, y
/// cómo se promueve al primer administrador (`POST /bootstrap`, la única acción de este
/// controller que NO exige [RequireRole("Admin")] — por diseño, es cómo se consigue el
/// primer admin).
/// </summary>
[ApiController]
[Route("api/v1/admin")]
public class AdminController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("users")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(PagedResponse<AdminUserSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AdminUserSummaryResponse>>> ListUsers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? email = null, [FromQuery] string? status = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListUsersQuery(page, pageSize, email, status), ct);
        return Ok(new PagedResponse<AdminUserSummaryResponse>(
            result.Items.Select(u => new AdminUserSummaryResponse(u.Id, u.Email, u.EmailVerified, u.Role, u.Status, u.CreatedAt)).ToList(),
            result.Page, result.PageSize, result.TotalCount));
    }

    [HttpGet("users/{id:guid}")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetailResponse>> GetUser(Guid id, CancellationToken ct)
    {
        var u = await mediator.Send(new GetUserDetailQuery(id), ct);
        return Ok(new AdminUserDetailResponse(
            u.Id, u.Email, u.EmailVerified, u.Role, u.Status, u.CreatedAt,
            u.Profile is null ? null : new AdminProfileSummaryResponse(u.Profile.FirstName, u.Profile.LastName, u.Profile.Timezone),
            u.Devices.Select(d => new AdminDeviceSummaryResponse(d.Id, d.Type, d.Status, d.LastSeenAt)).ToList(),
            u.Subscription is null ? null : new AdminSubscriptionSummaryResponse(u.Subscription.PlanCode, u.Subscription.Status, u.Subscription.StartedAt, u.Subscription.EndsAt)));
    }

    /// <summary>Suspender revoca todas las sesiones activas del usuario. Un admin no puede suspenderse a sí mismo.</summary>
    [HttpPut("users/{id:guid}/status")]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserStatus(Guid id, UpdateUserStatusRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateUserStatusCommand(currentUser.UserId!.Value, id, request.Status), ct);
        return NoContent();
    }

    [HttpGet("subscriptions")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(PagedResponse<AdminSubscriptionListItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AdminSubscriptionListItemResponse>>> ListSubscriptions(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListActiveSubscriptionsQuery(page, pageSize), ct);
        return Ok(new PagedResponse<AdminSubscriptionListItemResponse>(
            result.Items.Select(s => new AdminSubscriptionListItemResponse(s.SubscriptionId, s.UserEmail, s.PlanCode, s.StartedAt, s.EndsAt)).ToList(),
            result.Page, result.PageSize, result.TotalCount));
    }

    [HttpGet("stats")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AdminStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminStatsResponse>> GetStats(CancellationToken ct)
    {
        var stats = await mediator.Send(new GetStatsQuery(), ct);
        return Ok(new AdminStatsResponse(
            stats.TotalUsers, stats.ActiveUsersLast7Days,
            stats.UsersByPlan.Select(p => new PlanDistributionItemResponse(p.PlanCode, p.UserCount)).ToList()));
    }

    /// <summary>Historial de auditoría (Bloque 10) — filtrable por usuario y/o acción exacta, paginado.</summary>
    [HttpGet("audit-logs")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(PagedResponse<AuditLogItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<AuditLogItemResponse>>> ListAuditLogs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? userId = null, [FromQuery] string? action = null, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ListAuditLogsQuery(page, pageSize, userId, action), ct);
        return Ok(new PagedResponse<AuditLogItemResponse>(
            result.Items.Select(a => new AuditLogItemResponse(a.Id, a.UserId, a.UserEmail, a.Action, a.Metadata, a.IpAddress, a.CreatedAt)).ToList(),
            result.Page, result.PageSize, result.TotalCount));
    }

    /// <summary>
    /// Promueve al LLAMADOR (una cuenta ya registrada, con Bearer normal) a Admin — solo
    /// funciona si no existe ningún admin todavía Y si `secret` coincide con
    /// `Admin:BootstrapSecret` de la configuración del servidor. Deliberadamente
    /// `[Authorize]` simple (sin rol) — NO hereda [RequireRole("Admin")] porque ese
    /// atributo ahora vive por-acción, no a nivel de controller. Su propia lógica de
    /// negocio es la única protección adicional, ver HANDOFF.
    ///
    /// Nota de diseño: combinar `[AllowAnonymous]` con `[Authorize]` en la MISMA acción
    /// no logra "autenticado pero sin rol" — `[AllowAnonymous]` desactiva TODA
    /// autorización incondicionalmente, volviendo el endpoint completamente público. Por
    /// eso `[RequireRole("Admin")]` se movió del controller a cada acción individual en
    /// vez de intentar "desactivarlo" para esta acción con ese combo.
    /// </summary>
    [HttpPost("bootstrap")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Bootstrap(BootstrapAdminRequest request, CancellationToken ct)
    {
        await mediator.Send(new BootstrapFirstAdminCommand(currentUser.UserId!.Value, request.Secret), ct);
        return NoContent();
    }
}
