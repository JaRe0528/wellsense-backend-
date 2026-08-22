using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Auth.ResetPassword;

/// <summary>
/// Al resetear la contraseña se revocan TODOS los refresh tokens activos del usuario —
/// si alguien más tenía una sesión abierta (ej. el atacante que motivó el reset), queda
/// fuera. El usuario que hizo el reset debe volver a iniciar sesión.
/// </summary>
public class ResetPasswordCommandHandler(
    IWellSenseDbContext db,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IDateTimeProvider clock) : IRequestHandler<ResetPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var hash = tokenGenerator.Sha256Hex(request.Token);

        var token = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= clock.UtcNow)
            throw AuthDomainException.InvalidOrExpiredToken();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId && !u.IsDeleted, ct)
            ?? throw AuthDomainException.InvalidOrExpiredToken();

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = clock.UtcNow;
        token.UsedAt = clock.UtcNow;

        var activeRefreshTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var rt in activeRefreshTokens)
            rt.RevokedAt = clock.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "password_reset",
            Metadata = "{}",
            CreatedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
