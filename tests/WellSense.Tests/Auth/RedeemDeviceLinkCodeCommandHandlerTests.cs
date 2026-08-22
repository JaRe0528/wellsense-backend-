using FluentAssertions;
using Microsoft.Extensions.Configuration;
using WellSense.Application.Auth.DeviceLink;
using WellSense.Application.Common.Exceptions;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Auth;

public class RedeemDeviceLinkCodeCommandHandlerTests
{
    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "30"
        }).Build();

    [Fact]
    public async Task Redeem_with_valid_code_creates_device_and_issues_tokens()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h",
            EmailVerified = true, Status = UserStatus.Active, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Users.Add(user);
        var hasher = new FakeDeviceLinkCodeHasher();
        db.DeviceLinkCodes.Add(new DeviceLinkCode
        {
            Id = Guid.NewGuid(), UserId = user.Id, CodeHash = hasher.Hash("654321"),
            ExpiresAt = clock.UtcNow.AddMinutes(30), CreatedAt = clock.UtcNow, MaxAttempts = 5
        });
        await db.SaveChangesAsync();

        var handler = new RedeemDeviceLinkCodeCommandHandler(
            db, hasher, new FakeJwtTokenService(), new SequentialTokenGenerator(), clock, Config());

        var result = await handler.Handle(
            new RedeemDeviceLinkCodeCommand("654321", "Pixel 9", "Android 15", "1.0.0", "9.8.7.6"), default);

        result.UserId.Should().Be(user.Id);
        db.Devices.Single().Type.Should().Be(DeviceType.Phone);
        db.DeviceLinkCodes.Single().UsedAt.Should().NotBeNull();
        db.DeviceLinkCodes.Single().DeviceId.Should().Be(result.DeviceId);
        db.RefreshTokens.Should().ContainSingle(t => t.UserId == user.Id);
    }

    [Fact]
    public async Task Redeem_with_wrong_code_throws_generic_invalid_error()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var hasher = new FakeDeviceLinkCodeHasher();

        var handler = new RedeemDeviceLinkCodeCommandHandler(
            db, hasher, new FakeJwtTokenService(), new SequentialTokenGenerator(), clock, Config());

        var act = () => handler.Handle(
            new RedeemDeviceLinkCodeCommand("000000", null, null, null, "1.2.3.4"), default);

        await act.Should().ThrowAsync<AuthDomainException>().Where(e => e.ErrorCode == "INVALID_DEVICE_LINK_CODE");
    }

    [Fact]
    public async Task Redeem_with_expired_code_throws_same_generic_error()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "user@x.com", PasswordHash = "h",
            EmailVerified = true, Status = UserStatus.Active, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Users.Add(user);
        var hasher = new FakeDeviceLinkCodeHasher();
        db.DeviceLinkCodes.Add(new DeviceLinkCode
        {
            Id = Guid.NewGuid(), UserId = user.Id, CodeHash = hasher.Hash("111222"),
            ExpiresAt = clock.UtcNow.AddMinutes(-1), CreatedAt = clock.UtcNow.AddMinutes(-31), MaxAttempts = 5
        });
        await db.SaveChangesAsync();

        var handler = new RedeemDeviceLinkCodeCommandHandler(
            db, hasher, new FakeJwtTokenService(), new SequentialTokenGenerator(), clock, Config());

        var act = () => handler.Handle(
            new RedeemDeviceLinkCodeCommand("111222", null, null, null, "1.2.3.4"), default);

        await act.Should().ThrowAsync<AuthDomainException>().Where(e => e.ErrorCode == "INVALID_DEVICE_LINK_CODE");
    }
}
