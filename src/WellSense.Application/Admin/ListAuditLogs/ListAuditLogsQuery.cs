using MediatR;
using WellSense.Application.Common;

namespace WellSense.Application.Admin.ListAuditLogs;

public record ListAuditLogsQuery(int Page, int PageSize, Guid? UserId, string? Action) : IRequest<PagedResult<AuditLogItem>>;

public record AuditLogItem(Guid Id, Guid? UserId, string? UserEmail, string Action, string Metadata, string? IpAddress, DateTimeOffset CreatedAt);
