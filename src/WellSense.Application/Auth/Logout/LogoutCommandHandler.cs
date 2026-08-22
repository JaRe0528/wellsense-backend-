using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Auth.Logout;

public class LogoutCommandHandler(
    IWellSenseDbContext db,
    ITokenGenerator tokenGenerator,
    IDateTimeProvider clock) : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken ct)
    {
        var hash = tokenGenerator.Sha256Hex(request.RefreshToken);

        // Idempotente a propósito: si el token no existe, ya no pertenece al usuario,
        // o ya estaba revocado, el logout igual responde éxito — no hay información
        // de valor que proteger devolviendo un error aquí, y evita filtrar si un
        // token específico existe o no.
        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == request.CurrentUserId, ct);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Unit.Value;
    }
}
