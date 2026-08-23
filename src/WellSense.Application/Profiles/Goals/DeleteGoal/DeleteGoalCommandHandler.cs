using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Profiles.Goals.DeleteGoal;

public class DeleteGoalCommandHandler(IWellSenseDbContext db) : IRequestHandler<DeleteGoalCommand, Unit>
{
    public async Task<Unit> Handle(DeleteGoalCommand request, CancellationToken ct)
    {
        // El join contra profiles.user_id es la autorización: un goal solo se puede
        // borrar si pertenece al perfil del usuario autenticado — nunca confiar en que
        // el GoalId de la URL "obviamente" es del usuario correcto.
        var goal = await db.Goals
            .Join(db.Profiles, g => g.ProfileId, p => p.Id, (g, p) => new { g, p })
            .Where(x => x.g.Id == request.GoalId && x.p.UserId == request.CurrentUserId)
            .Select(x => x.g)
            .FirstOrDefaultAsync(ct);

        if (goal is null)
            throw new KeyNotFoundException("Meta no encontrada.");

        db.Goals.Remove(goal);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
