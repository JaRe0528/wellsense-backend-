using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Admin.ListAuditLogs;

/// <summary>
/// Para que el panel de Admin (Bloque 9) pueda mostrar el historial de auditoría que
/// este mismo bloque terminó de poblar (ver §1/§2 del HANDOFF de Bloque 10 sobre qué
/// acciones se registran). Filtro por usuario y/o por acción exacta, paginado — nunca
/// trae toda la tabla de una sola vez.
/// </summary>
public class ListAuditLogsQueryHandler(IWellSenseDbContext db)
    : IRequestHandler<ListAuditLogsQuery, PagedResult<AuditLogItem>>
{
    public async Task<PagedResult<AuditLogItem>> Handle(ListAuditLogsQuery request, CancellationToken ct)
    {
        var query = db.AuditLogs.AsQueryable();

        if (request.UserId is not null)
            query = query.Where(a => a.UserId == request.UserId);
        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(a => a.Action == request.Action);

        var totalCount = await query.CountAsync(ct);

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        if (logs.Count == 0)
            return new PagedResult<AuditLogItem>([], request.Page, request.PageSize, totalCount);

        var userIds = logs.Where(l => l.UserId is not null).Select(l => l.UserId!.Value).Distinct().ToList();
        var userEmails = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        var items = logs
            .Select(l => new AuditLogItem(
                l.Id, l.UserId, l.UserId is not null ? userEmails.GetValueOrDefault(l.UserId.Value) : null,
                l.Action, l.Metadata, l.IpAddress, l.CreatedAt))
            .ToList();

        return new PagedResult<AuditLogItem>(items, request.Page, request.PageSize, totalCount);
    }
}
