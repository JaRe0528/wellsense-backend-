using FluentAssertions;
using WellSense.Application.Admin.ListAuditLogs;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Admin;

public class ListAuditLogsQueryHandlerTests
{
    [Fact]
    public async Task Filters_by_user_and_resolves_the_email()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User { Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.Add(user);
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), UserId = user.Id, Action = "login_succeeded", Metadata = "{}", CreatedAt = clock.UtcNow });
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Action = "login_succeeded", Metadata = "{}", CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();
        var handler = new ListAuditLogsQueryHandler(db);

        var result = await handler.Handle(new ListAuditLogsQuery(1, 20, user.Id, null), default);

        result.Items.Should().ContainSingle();
        result.Items.Single().UserEmail.Should().Be("user@x.com");
    }

    [Fact]
    public async Task Filters_by_action()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), Action = "login_succeeded", Metadata = "{}", CreatedAt = clock.UtcNow });
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), Action = "login_failed", Metadata = "{}", CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();
        var handler = new ListAuditLogsQueryHandler(db);

        var result = await handler.Handle(new ListAuditLogsQuery(1, 20, null, "login_failed"), default);

        result.Items.Should().ContainSingle(a => a.Action == "login_failed");
    }

    [Fact]
    public async Task Handles_entries_with_no_user_id_gracefully()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), UserId = null, Action = "login_failed", Metadata = "{}", CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync();
        var handler = new ListAuditLogsQueryHandler(db);

        var result = await handler.Handle(new ListAuditLogsQuery(1, 20, null, null), default);

        result.Items.Single().UserEmail.Should().BeNull();
    }
}
