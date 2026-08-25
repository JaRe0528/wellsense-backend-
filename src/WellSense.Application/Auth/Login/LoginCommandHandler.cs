using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Auth.Login;

/// <summary>
/// Modificado en Bloque 10 (auditoría completa): se agregó registro en `audit_logs`
/// tanto para login exitoso como fallido — ninguno de los dos se registraba antes. La
/// lógica de autenticación en sí (verificación de contraseña, chequeos de status/email
/// verificado, emisión de tokens) NO se tocó, solo se agregaron las llamadas a
/// `LogFailedLoginAsync`/el registro de éxito al final, reutilizando el mismo patrón que
/// ya existía para `refresh_token_reuse_detected` (Bloque 2, RefreshTokenCommandHandler).
///
/// Un intento fallido con un email que no existe se registra con `UserId = null` (no hay
/// a quién atribuirlo) — nunca se revela en el registro si el email existía o no más
/// allá de eso, mismo principio de "mismo error para ambos casos" que ya regía la
/// respuesta HTTP desde Bloque 2.
/// </summary>
public class LoginCommandHandler(
    IWellSenseDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ITokenGenerator tokenGenerator,
    IDateTimeProvider clock,
    IConfiguration configuration) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(
            u => !u.IsDeleted && u.Email.ToLower() == normalizedEmail, ct);

        // Mismo mensaje/código de error tanto si el usuario no existe como si la
        // contraseña es incorrecta — no dar pistas de qué emails están registrados.
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await LogFailedLoginAsync(user?.Id, "invalid_credentials", request.IpAddress, ct);
            throw AuthDomainException.InvalidCredentials();
        }

        if (user.Status != UserStatus.Active)
        {
            await LogFailedLoginAsync(user.Id, "account_not_active", request.IpAddress, ct);
            throw AuthDomainException.AccountNotActive();
        }

        if (!user.EmailVerified)
        {
            await LogFailedLoginAsync(user.Id, "email_not_verified", request.IpAddress, ct);
            throw AuthDomainException.EmailNotVerified();
        }

        var accessMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);
        var refreshDays = configuration.GetValue("Jwt:RefreshTokenDays", 30);

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var rawRefreshToken = tokenGenerator.GenerateUrlSafeToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenGenerator.Sha256Hex(rawRefreshToken),
            ExpiresAt = clock.UtcNow.AddDays(refreshDays),
            CreatedByIp = request.IpAddress,
            CreatedAt = clock.UtcNow
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "login_succeeded",
            Metadata = "{}",
            IpAddress = request.IpAddress,
            CreatedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);

        return new LoginResult(
            accessToken,
            rawRefreshToken,
            clock.UtcNow.AddMinutes(accessMinutes),
            user.Id,
            user.Email,
            user.EmailVerified);
    }

    private async Task LogFailedLoginAsync(Guid? userId, string reason, string? ipAddress, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "login_failed",
            Metadata = JsonSerializer.Serialize(new { reason }),
            IpAddress = ipAddress,
            CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
