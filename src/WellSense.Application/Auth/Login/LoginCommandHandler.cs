using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Auth.Login;

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
            throw AuthDomainException.InvalidCredentials();

        if (user.Status != UserStatus.Active)
            throw AuthDomainException.AccountNotActive();

        if (!user.EmailVerified)
            throw AuthDomainException.EmailNotVerified();

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

        await db.SaveChangesAsync(ct);

        return new LoginResult(
            accessToken,
            rawRefreshToken,
            clock.UtcNow.AddMinutes(accessMinutes),
            user.Id,
            user.Email,
            user.EmailVerified);
    }
}
