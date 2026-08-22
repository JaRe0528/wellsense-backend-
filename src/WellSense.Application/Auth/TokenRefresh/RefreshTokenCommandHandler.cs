using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Auth.TokenRefresh;

/// <summary>
/// Rotación de refresh token con detección de reuse. Regla: un refresh token solo se
/// puede canjear una vez. Si llega un token que ya fue revocado (porque ya se usó para
/// rotar, o porque se cerró sesión con él), se trata como indicio de robo: se revoca
/// TODA la cadena de refresh tokens activos del usuario, forzando reautenticación en
/// todos los dispositivos, y se registra en audit_logs.
/// </summary>
public class RefreshTokenCommandHandler(
    IWellSenseDbContext db,
    IJwtTokenService jwtTokenService,
    ITokenGenerator tokenGenerator,
    IDateTimeProvider clock,
    IConfiguration configuration,
    ILogger<RefreshTokenCommandHandler> logger) : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var incomingHash = tokenGenerator.Sha256Hex(request.RefreshToken);

        var existing = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == incomingHash, ct);

        if (existing is null)
            throw AuthDomainException.InvalidOrReusedRefreshToken();

        if (existing.RevokedAt is not null)
        {
            // Reuse detectado: este token ya fue rotado o revocado explícitamente y
            // alguien lo está presentando de nuevo. Revocar toda la cadena activa del
            // usuario (defensa en profundidad — no sabemos si el atacante ya tiene
            // también el token más nuevo, pero cortamos todo lo que sí controlamos).
            var activeTokens = await db.RefreshTokens
                .Where(t => t.UserId == existing.UserId && t.RevokedAt == null)
                .ToListAsync(ct);

            foreach (var t in activeTokens)
                t.RevokedAt = clock.UtcNow;

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = existing.UserId,
                Action = "refresh_token_reuse_detected",
                Metadata = $"{{\"revoked_token_id\":\"{existing.Id}\",\"revoked_chain_count\":{activeTokens.Count}}}",
                IpAddress = request.IpAddress,
                CreatedAt = clock.UtcNow
            });

            await db.SaveChangesAsync(ct);

            logger.LogWarning("Reuse de refresh token detectado para user {UserId} — cadena revocada", existing.UserId);
            throw AuthDomainException.InvalidOrReusedRefreshToken();
        }

        if (existing.ExpiresAt <= clock.UtcNow)
            throw AuthDomainException.InvalidOrReusedRefreshToken();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId && !u.IsDeleted, ct);
        if (user is null || user.Status != UserStatus.Active)
            throw AuthDomainException.InvalidOrReusedRefreshToken();

        var refreshDays = configuration.GetValue("Jwt:RefreshTokenDays", 30);
        var accessMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);

        var newRefreshRaw = tokenGenerator.GenerateUrlSafeToken();
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenGenerator.Sha256Hex(newRefreshRaw),
            ExpiresAt = clock.UtcNow.AddDays(refreshDays),
            CreatedByIp = request.IpAddress,
            CreatedAt = clock.UtcNow
        };
        db.RefreshTokens.Add(newRefreshToken);

        existing.RevokedAt = clock.UtcNow;
        existing.ReplacedByTokenId = newRefreshToken.Id;

        await db.SaveChangesAsync(ct);

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());

        return new RefreshTokenResult(accessToken, newRefreshRaw, clock.UtcNow.AddMinutes(accessMinutes));
    }
}
