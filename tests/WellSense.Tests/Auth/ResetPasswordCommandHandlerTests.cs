using FluentAssertions;
using WellSense.Application.Auth.ResetPassword;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Auth;

public class ResetPasswordCommandHandlerTests
{
    [Fact]
    public async Task Reset_password_revokes_all_active_refresh_tokens()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var hasher = new PlainTextPasswordHasher();
        var tokens = new SequentialTokenGenerator();

        var user = new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = hasher.Hash("Old12345"),
            CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Users.Add(user);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = "active-1",
            ExpiresAt = clock.UtcNow.AddDays(10), CreatedAt = clock.UtcNow
        });

        var rawResetToken = tokens.GenerateUrlSafeToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = tokens.Sha256Hex(rawResetToken),
            ExpiresAt = clock.UtcNow.AddHours(1)
        });
        await db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(db, hasher, tokens, clock);

        await handler.Handle(new ResetPasswordCommand(rawResetToken, "NewPass1234"), default);

        hasher.Verify("NewPass1234", db.Users.Single().PasswordHash).Should().BeTrue();
        db.RefreshTokens.Single().RevokedAt.Should().NotBeNull();
        db.PasswordResetTokens.Single().UsedAt.Should().NotBeNull();
        db.AuditLogs.Should().Contain(a => a.Action == "password_reset");
    }
}
