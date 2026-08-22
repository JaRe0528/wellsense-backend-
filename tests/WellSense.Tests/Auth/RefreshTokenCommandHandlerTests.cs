using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WellSense.Application.Auth.TokenRefresh;
using WellSense.Application.Common.Exceptions;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Auth;

public class RefreshTokenCommandHandlerTests
{
    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "30"
        }).Build();

    private static (WellSense.Infrastructure.Persistence.WellSenseDbContext db, User user, RefreshToken token, SequentialTokenGenerator tokens)
        SeedActiveUserWithRefreshToken(FixedClock clock)
    {
        var db = InMemoryDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h",
            EmailVerified = true, Status = UserStatus.Active, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Users.Add(user);

        var tokens = new SequentialTokenGenerator();
        var rawToken = tokens.GenerateUrlSafeToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = tokens.Sha256Hex(rawToken),
            ExpiresAt = clock.UtcNow.AddDays(30), CreatedAt = clock.UtcNow
        };
        db.RefreshTokens.Add(refreshToken);
        db.SaveChangesAsync().GetAwaiter().GetResult();

        return (db, user, refreshToken, tokens);
    }

    [Fact]
    public async Task Refresh_with_valid_token_rotates_and_revokes_old_token()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var (db, user, oldToken, tokens) = SeedActiveUserWithRefreshToken(clock);
        var rawOldToken = "token-1"; // el primero que emite SequentialTokenGenerator

        var handler = new RefreshTokenCommandHandler(
            db, new FakeJwtTokenService(), tokens, clock, Config(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var result = await handler.Handle(new RefreshTokenCommand(rawOldToken, "1.2.3.4"), default);

        result.RefreshToken.Should().NotBe(rawOldToken);

        var refreshed = db.RefreshTokens.Single(t => t.Id == oldToken.Id);
        refreshed.RevokedAt.Should().NotBeNull();
        refreshed.ReplacedByTokenId.Should().NotBeNull();

        db.RefreshTokens.Should().HaveCount(2);
        var newToken = db.RefreshTokens.Single(t => t.Id == refreshed.ReplacedByTokenId);
        newToken.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Refresh_with_already_revoked_token_revokes_entire_chain_and_throws()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var (db, user, oldToken, tokens) = SeedActiveUserWithRefreshToken(clock);
        var rawOldToken = "token-1";

        // Simula que el token YA fue usado una vez (rotado) — ahora alguien (un atacante
        // que interceptó el token viejo) intenta reusarlo.
        oldToken.RevokedAt = clock.UtcNow;
        var otherActiveToken = new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = "otro-hash-activo",
            ExpiresAt = clock.UtcNow.AddDays(10), CreatedAt = clock.UtcNow
        };
        db.RefreshTokens.Add(otherActiveToken);
        await db.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(
            db, new FakeJwtTokenService(), tokens, clock, Config(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var act = () => handler.Handle(new RefreshTokenCommand(rawOldToken, "1.2.3.4"), default);

        await act.Should().ThrowAsync<AuthDomainException>().Where(e => e.ErrorCode == "INVALID_REFRESH_TOKEN");

        // La cadena completa del usuario queda revocada, no solo el token reusado.
        db.RefreshTokens.Single(t => t.Id == otherActiveToken.Id).RevokedAt.Should().NotBeNull();
        db.AuditLogs.Should().Contain(a => a.Action == "refresh_token_reuse_detected" && a.UserId == user.Id);
    }

    [Fact]
    public async Task Refresh_with_expired_token_throws()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var (db, user, oldToken, tokens) = SeedActiveUserWithRefreshToken(clock);
        oldToken.ExpiresAt = clock.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var handler = new RefreshTokenCommandHandler(
            db, new FakeJwtTokenService(), tokens, clock, Config(),
            NullLogger<RefreshTokenCommandHandler>.Instance);

        var act = () => handler.Handle(new RefreshTokenCommand("token-1", null), default);

        await act.Should().ThrowAsync<AuthDomainException>().Where(e => e.ErrorCode == "INVALID_REFRESH_TOKEN");
    }
}
