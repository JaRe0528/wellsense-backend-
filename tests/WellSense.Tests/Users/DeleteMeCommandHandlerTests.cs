using FluentAssertions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Users.DeleteMe;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Users;

public class DeleteMeCommandHandlerTests
{
    [Fact]
    public async Task Soft_deletes_account_and_revokes_active_refresh_tokens()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var hasher = new PlainTextPasswordHasher();
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = hasher.Hash("Password123"),
            CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Users.Add(user);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = "active-1",
            ExpiresAt = clock.UtcNow.AddDays(10), CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new DeleteMeCommandHandler(db, hasher, clock);
        await handler.Handle(new DeleteMeCommand(user.Id, "Password123"), default);

        var deletedUser = db.Users.Single();
        deletedUser.IsDeleted.Should().BeTrue();
        deletedUser.DeletedAt.Should().NotBeNull();
        db.RefreshTokens.Single().RevokedAt.Should().NotBeNull();
        db.AuditLogs.Should().Contain(a => a.Action == "account_deleted");
    }

    [Fact]
    public async Task Wrong_password_throws_and_does_not_delete()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var hasher = new PlainTextPasswordHasher();
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = hasher.Hash("Correct123"),
            CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new DeleteMeCommandHandler(db, hasher, clock);
        var act = () => handler.Handle(new DeleteMeCommand(user.Id, "Wrong123"), default);

        await act.Should().ThrowAsync<AuthDomainException>().Where(e => e.ErrorCode == "INVALID_CREDENTIALS");
        db.Users.Single().IsDeleted.Should().BeFalse();
    }
}
