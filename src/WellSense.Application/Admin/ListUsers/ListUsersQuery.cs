using MediatR;
using WellSense.Application.Common;

namespace WellSense.Application.Admin.ListUsers;

public record ListUsersQuery(int Page, int PageSize, string? EmailFilter, string? StatusFilter) : IRequest<PagedResult<AdminUserSummary>>;

public record AdminUserSummary(Guid Id, string Email, bool EmailVerified, string Role, string Status, DateTimeOffset CreatedAt);
