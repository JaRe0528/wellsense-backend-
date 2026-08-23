using FluentAssertions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Users.GetMe;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Users;

public class GetMeQueryHandlerTests
{
    [Fact]
    public async Task Returns_current_user_data()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h", EmailVerified = true,
            Role = UserRole.User, Status = UserStatus.Active, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new GetMeQueryHandler(db);
        var result = await handler.Handle(new GetMeQuery(user.Id), default);

        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("user@x.com");
        result.Role.Should().Be("User");
    }

    [Fact]
    public async Task Throws_when_user_soft_deleted()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "gone@x.com", PasswordHash = "h", IsDeleted = true,
            CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new GetMeQueryHandler(db);
        var act = () => handler.Handle(new GetMeQuery(user.Id), default);

        await act.Should().ThrowAsync<AuthDomainException>().Where(e => e.ErrorCode == "ACCOUNT_NOT_FOUND");
    }
}
