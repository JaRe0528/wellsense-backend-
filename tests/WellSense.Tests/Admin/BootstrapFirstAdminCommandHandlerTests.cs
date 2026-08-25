using FluentAssertions;
using Microsoft.Extensions.Configuration;
using WellSense.Application.Admin.BootstrapFirstAdmin;
using WellSense.Application.Common.Exceptions;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Admin;

public class BootstrapFirstAdminCommandHandlerTests
{
    private static IConfiguration ConfigWithSecret(string? secret) => new ConfigurationBuilder()
        .AddInMemoryCollection(secret is null
            ? []
            : new Dictionary<string, string?> { ["Admin:BootstrapSecret"] = secret })
        .Build();

    [Fact]
    public async Task Promotes_the_caller_when_secret_matches_and_no_admin_exists_yet()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User { Id = Guid.NewGuid(), Email = "future-admin@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var handler = new BootstrapFirstAdminCommandHandler(db, ConfigWithSecret("correct-secret"), clock);

        await handler.Handle(new BootstrapFirstAdminCommand(user.Id, "correct-secret"), default);

        db.Users.Single().Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Wrong_secret_is_rejected_even_when_bootstrap_would_otherwise_be_allowed()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User { Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var handler = new BootstrapFirstAdminCommandHandler(db, ConfigWithSecret("correct-secret"), clock);

        var act = () => handler.Handle(new BootstrapFirstAdminCommand(user.Id, "wrong-secret"), default);

        await act.Should().ThrowAsync<AdminDomainException>().Where(e => e.ErrorCode == "INVALID_BOOTSTRAP_SECRET");
        db.Users.Single().Role.Should().Be(UserRole.User); // no se tocó
    }

    [Fact]
    public async Task Empty_configured_secret_never_matches_anything()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User { Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        // Secreto NO configurado en absoluto — el bootstrap debe quedar efectivamente
        // deshabilitado, nunca tratarse un string vacío como "válido".
        var handler = new BootstrapFirstAdminCommandHandler(db, ConfigWithSecret(null), clock);

        var act = () => handler.Handle(new BootstrapFirstAdminCommand(user.Id, ""), default);

        await act.Should().ThrowAsync<AdminDomainException>().Where(e => e.ErrorCode == "INVALID_BOOTSTRAP_SECRET");
    }

    [Fact]
    public async Task Second_attempt_after_an_admin_already_exists_is_rejected_even_with_the_correct_secret()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var existingAdmin = new User { Id = Guid.NewGuid(), Email = "admin@x.com", PasswordHash = "h", Role = UserRole.Admin, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        var anotherUser = new User { Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h", CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Users.AddRange(existingAdmin, anotherUser);
        await db.SaveChangesAsync();
        var handler = new BootstrapFirstAdminCommandHandler(db, ConfigWithSecret("correct-secret"), clock);

        var act = () => handler.Handle(new BootstrapFirstAdminCommand(anotherUser.Id, "correct-secret"), default);

        await act.Should().ThrowAsync<AdminDomainException>().Where(e => e.ErrorCode == "ALREADY_BOOTSTRAPPED");
        db.Users.Single(u => u.Id == anotherUser.Id).Role.Should().Be(UserRole.User); // sigue sin ser admin
    }
}
