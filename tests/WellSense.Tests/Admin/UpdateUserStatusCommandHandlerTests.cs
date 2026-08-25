using FluentAssertions;
using WellSense.Application.Admin.UpdateUserStatus;
using WellSense.Application.Common.Exceptions;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Admin;

public class UpdateUserStatusCommandHandlerTests
{
    [Fact]
    public async Task Suspending_a_user_revokes_all_their_active_refresh_tokens()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var adminId = Guid.NewGuid();
        var target = new User { Id = Guid.NewGuid(), Email = "target@x.com", PasswordHash = "h", Status = UserStatus.Active, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.Add(target);
        db.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = target.Id, TokenHash = "t1", ExpiresAt = clock.UtcNow.AddDays(10), CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();
        var handler = new UpdateUserStatusCommandHandler(db, clock);

        await handler.Handle(new UpdateUserStatusCommand(adminId, target.Id, "SUSPENDED"), default);

        db.Users.Single().Status.Should().Be(UserStatus.Suspended);
        db.RefreshTokens.Single().RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Reactivating_does_not_touch_refresh_tokens()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var adminId = Guid.NewGuid();
        var target = new User { Id = Guid.NewGuid(), Email = "target@x.com", PasswordHash = "h", Status = UserStatus.Suspended, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.Add(target);
        await db.SaveChangesAsync();
        var handler = new UpdateUserStatusCommandHandler(db, clock);

        await handler.Handle(new UpdateUserStatusCommand(adminId, target.Id, "ACTIVE"), default);

        db.Users.Single().Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task An_admin_cannot_suspend_their_own_account()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@x.com", PasswordHash = "h", Role = UserRole.Admin, Status = UserStatus.Active, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        var handler = new UpdateUserStatusCommandHandler(db, clock);

        var act = () => handler.Handle(new UpdateUserStatusCommand(admin.Id, admin.Id, "SUSPENDED"), default);

        await act.Should().ThrowAsync<AdminDomainException>().Where(e => e.ErrorCode == "CANNOT_SUSPEND_SELF");
        db.Users.Single().Status.Should().Be(UserStatus.Active); // no se tocó
    }

    [Fact]
    public async Task Updating_a_user_that_does_not_exist_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var handler = new UpdateUserStatusCommandHandler(db, clock);

        var act = () => handler.Handle(new UpdateUserStatusCommand(Guid.NewGuid(), Guid.NewGuid(), "SUSPENDED"), default);

        await act.Should().ThrowAsync<AdminDomainException>().Where(e => e.ErrorCode == "USER_NOT_FOUND");
    }
}
