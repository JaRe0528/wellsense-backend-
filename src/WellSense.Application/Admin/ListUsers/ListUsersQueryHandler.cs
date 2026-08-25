using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Admin.ListUsers;

public class ListUsersQueryHandler(IWellSenseDbContext db) : IRequestHandler<ListUsersQuery, PagedResult<AdminUserSummary>>
{
    public async Task<PagedResult<AdminUserSummary>> Handle(ListUsersQuery request, CancellationToken ct)
    {
        var query = db.Users.Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.EmailFilter))
            query = query.Where(u => u.Email.Contains(request.EmailFilter.ToLower()));

        // El filtro por status SÍ es seguro de traducir a SQL en un WHERE (comparación
        // de igualdad contra el converter, no un .ToString() dentro del proyectado) —
        // distinto del riesgo ya documentado en ListMyDevicesQueryHandler (Bloque 4).
        if (request.StatusFilter is not null)
        {
            var status = Enum.Parse<WellSense.Domain.Identity.UserStatus>(request.StatusFilter, true);
            query = query.Where(u => u.Status == status);
        }

        var totalCount = await query.CountAsync(ct);

        // Se materializa la página antes de traducir Role/Status a string — mismo
        // motivo de siempre (ambos usan HasConversion con lambdas propias).
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = users
            .Select(u => new AdminUserSummary(
                u.Id, u.Email, u.EmailVerified, u.Role.ToString().ToUpperInvariant(), u.Status.ToString().ToUpperInvariant(), u.CreatedAt))
            .ToList();

        return new PagedResult<AdminUserSummary>(items, request.Page, request.PageSize, totalCount);
    }
}
