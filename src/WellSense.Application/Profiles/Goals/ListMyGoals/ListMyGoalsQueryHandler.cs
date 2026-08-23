using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Profiles.Goals.ListMyGoals;

public class ListMyGoalsQueryHandler(IWellSenseDbContext db) : IRequestHandler<ListMyGoalsQuery, IReadOnlyList<GoalResult>>
{
    public async Task<IReadOnlyList<GoalResult>> Handle(ListMyGoalsQuery request, CancellationToken ct)
    {
        // Join directo por user_id vía profiles — no asumimos que el perfil ya existe
        // (si no existe, tampoco puede tener goals; devolvemos lista vacía sin crear
        // el perfil de paso, a diferencia de GetMyProfile, porque aquí no hace falta).
        return await db.Goals
            .Where(g => db.Profiles.Any(p => p.Id == g.ProfileId && p.UserId == request.CurrentUserId))
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GoalResult(g.Id, g.Type, g.TargetValue, g.CreatedAt))
            .ToListAsync(ct);
    }
}
