using FluentAssertions;
using WellSense.Application.Admin.ListUsers;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Admin;

public class ListUsersQueryHandlerTests
{
    private static User MakeUser(string email, UserStatus status, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), Email = email, PasswordHash = "h", Status = status, CreatedAt = now, UpdatedAt = now
    };

    [Fact]
    public async Task Paginates_and_orders_newest_first()
    {
        using var db = InMemoryDbContextFactory.Create();
        var now = DateTimeOffset.UtcNow;
        db.Users.Add(MakeUser("a@x.com", UserStatus.Active, now.AddMinutes(-10)));
        db.Users.Add(MakeUser("b@x.com", UserStatus.Active, now));
        await db.SaveChangesAsync();
        var handler = new ListUsersQueryHandler(db);

        var result = await handler.Handle(new ListUsersQuery(1, 1, null, null), default);

        result.TotalCount.Should().Be(2);
        result.Items.Should().ContainSingle(u => u.Email == "b@x.com"); // más reciente primero, página de tamaño 1
    }

    [Fact]
    public async Task Filters_by_email_substring_case_insensitively()
    {
        using var db = InMemoryDbContextFactory.Create();
        var now = DateTimeOffset.UtcNow;
        db.Users.Add(MakeUser("ana@example.com", UserStatus.Active, now));
        db.Users.Add(MakeUser("beto@example.com", UserStatus.Active, now));
        await db.SaveChangesAsync();
        var handler = new ListUsersQueryHandler(db);

        var result = await handler.Handle(new ListUsersQuery(1, 20, "ANA", null), default);

        result.Items.Should().ContainSingle(u => u.Email == "ana@example.com");
    }

    [Fact]
    public async Task Filters_by_status()
    {
        using var db = InMemoryDbContextFactory.Create();
        var now = DateTimeOffset.UtcNow;
        db.Users.Add(MakeUser("active@x.com", UserStatus.Active, now));
        db.Users.Add(MakeUser("suspended@x.com", UserStatus.Suspended, now));
        await db.SaveChangesAsync();
        var handler = new ListUsersQueryHandler(db);

        var result = await handler.Handle(new ListUsersQuery(1, 20, null, "SUSPENDED"), default);

        result.Items.Should().ContainSingle(u => u.Email == "suspended@x.com");
    }

    [Fact]
    public async Task Excludes_soft_deleted_users()
    {
        using var db = InMemoryDbContextFactory.Create();
        var now = DateTimeOffset.UtcNow;
        var deleted = MakeUser("gone@x.com", UserStatus.Active, now);
        deleted.IsDeleted = true;
        db.Users.Add(deleted);
        await db.SaveChangesAsync();
        var handler = new ListUsersQueryHandler(db);

        var result = await handler.Handle(new ListUsersQuery(1, 20, null, null), default);

        result.Items.Should().BeEmpty();
    }
}
