using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WellSense.Application.Auth.Register;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Auth;

public class RegisterCommandHandlerTests
{
    [Fact]
    public async Task Register_creates_user_and_email_verification_token()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.Parse("2026-08-22T10:00:00Z"));
        var tokens = new SequentialTokenGenerator();
        var emailSender = Substitute.For<IEmailSender>();

        var handler = new RegisterCommandHandler(db, new PlainTextPasswordHasher(), tokens, emailSender, clock);

        var result = await handler.Handle(new RegisterCommand("Nueva@Ejemplo.com", "Password123"), default);

        var user = db.Users.Single();
        user.Email.Should().Be("nueva@ejemplo.com"); // normalizado a minúsculas
        user.EmailVerified.Should().BeFalse();
        db.EmailVerificationTokens.Single().UserId.Should().Be(user.Id);
        result.UserId.Should().Be(user.Id);
        await emailSender.Received(1).SendEmailVerificationAsync(user.Email, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_with_existing_email_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var hasher = new PlainTextPasswordHasher();
        db.Users.Add(new WellSense.Domain.Identity.User
        {
            Id = Guid.NewGuid(), Email = "ya@existe.com", PasswordHash = hasher.Hash("x"),
            CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new RegisterCommandHandler(
            db, hasher, new SequentialTokenGenerator(), Substitute.For<IEmailSender>(), clock);

        var act = () => handler.Handle(new RegisterCommand("ya@existe.com", "Password123"), default);

        await act.Should().ThrowAsync<AuthDomainException>().Where(e => e.ErrorCode == "EMAIL_ALREADY_REGISTERED");
    }
}
