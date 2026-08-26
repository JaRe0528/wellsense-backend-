using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Auth.Register;

public class RegisterCommandHandler(
    IWellSenseDbContext db,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IDateTimeProvider clock) : IRequestHandler<RegisterCommand, RegisterResult>
{
    // Sin nombre para el saludo del correo — todavía no existe ningún Profile en este
    // punto (se crea de forma perezosa más adelante, Bloque 3); SmtpEmailSender cae a
    // usar el email como saludo cuando recibe null.
    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // La unicidad real la garantiza ux_users_email_lower (lower(email) WHERE is_deleted=false)
        // a nivel de motor; este chequeo previo solo evita una excepción de BD en el
        // camino feliz y da un error de negocio claro (409) en vez de un 500 genérico.
        var exists = await db.Users.AnyAsync(u => !u.IsDeleted && u.Email.ToLower() == normalizedEmail, ct);
        if (exists)
            throw AuthDomainException.EmailAlreadyRegistered();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            PasswordAlgo = "argon2id",
            EmailVerified = false,
            Role = UserRole.User,
            Status = UserStatus.Active,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
            IsDeleted = false
        };
        db.Users.Add(user);

        var rawToken = tokenGenerator.GenerateUrlSafeToken();
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenGenerator.Sha256Hex(rawToken),
            ExpiresAt = clock.UtcNow.AddHours(24)
        });

        await db.SaveChangesAsync(ct);

        await emailSender.SendEmailVerificationAsync(user.Email, recipientName: null, rawToken, ct);

        return new RegisterResult(user.Id, user.Email);
    }
}
