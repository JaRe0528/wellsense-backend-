using FluentAssertions;
using Microsoft.Extensions.Configuration;
using WellSense.Application.Auth.Login;
using WellSense.Application.Common.Exceptions;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Auth;

public class LoginCommandHandlerTests
{
    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "30"
        }).Build();

    [Fact]
    public async Task Login_with_correct_credentials_and_verified_email_returns_tokens()
    {
        using var db = InMemoryDbContextFactory.Create();
        var hasher = new PlainTextPasswordHasher();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = hasher.Hash("Password123"),
            EmailVerified = true, Status = UserStatus.Active, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(
            db, hasher, new FakeJwtTokenService(), new SequentialTokenGenerator(), clock, Config());

        var result = await handler.Handle(new LoginCommand("user@x.com", "Password123", "127.0.0.1"), default);

        result.UserId.Should().Be(user.Id);
        db.RefreshTokens.Single().UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Login_with_wrong_password_throws_invalid_credentials()
    {
        using var db = InMemoryDbContextFactory.Create();
        var hasher = new PlainTextPasswordHasher();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = hasher.Hash("Correct123"),
            EmailVerified = true, Status = UserStatus.Active, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(
            db, hasher, new FakeJwtTokenService(), new SequentialTokenGenerator(), clock, Config());

        var act = () => handler.Handle(new LoginCommand("user@x.com", "Wrong123", null), default);

        await act.Should().ThrowAsync<AuthDomainException>().Where(e => e.ErrorCode == "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_with_unverified_email_throws_email_not_verified()
    {
        using var db = InMemoryDbContextFactory.Create();
        var hasher = new PlainTextPasswordHasher();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = hasher.Hash("Password123"),
            EmailVerified = false, Status = UserStatus.Active, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(
            db, hasher, new FakeJwtTokenService(), new SequentialTokenGenerator(), clock, Config());

        var act = () => handler.Handle(new LoginCommand("user@x.com", "Password123", null), default);

        await act.Should().ThrowAsync<AuthDomainException>().Where(e => e.ErrorCode == "EMAIL_NOT_VERIFIED");
    }
}
