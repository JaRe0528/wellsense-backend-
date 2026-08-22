using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WellSense.Application.Auth.DeviceLink;
using WellSense.Domain.Devices;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Auth;

public class GenerateDeviceLinkCodeCommandHandlerTests
{
    [Fact]
    public async Task Generate_deletes_previous_unused_code_of_same_user()
    {
        using var inMemory = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        inMemory.DeviceLinkCodes.Add(new DeviceLinkCode
        {
            Id = Guid.NewGuid(), UserId = userId, CodeHash = "hmac::111111",
            ExpiresAt = clock.UtcNow.AddMinutes(30), CreatedAt = clock.UtcNow
        });
        await inMemory.SaveChangesAsync();

        var db = new ThrowingDbContextDecorator(inMemory, failFirstNCalls: 0);
        var handler = new GenerateDeviceLinkCodeCommandHandler(
            db, new SequentialTokenGenerator(), new FakeDeviceLinkCodeHasher(),
            new ControllableViolationDetector(alwaysReturn: false), clock,
            NullLogger<GenerateDeviceLinkCodeCommandHandler>.Instance);

        await handler.Handle(new GenerateDeviceLinkCodeCommand(userId), default);

        var remaining = inMemory.DeviceLinkCodes.Where(c => c.UserId == userId).ToList();
        remaining.Should().HaveCount(1);
        remaining.Single().CodeHash.Should().NotBe("hmac::111111");
    }

    [Fact]
    public async Task Generate_retries_on_simulated_collision_and_succeeds()
    {
        using var inMemory = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        // Falla las primeras 2 veces (simulando colisión de hash con otro usuario),
        // la 3ra debe tener éxito.
        var db = new ThrowingDbContextDecorator(inMemory, failFirstNCalls: 2);

        var handler = new GenerateDeviceLinkCodeCommandHandler(
            db, new SequentialTokenGenerator(), new FakeDeviceLinkCodeHasher(),
            new ControllableViolationDetector(alwaysReturn: true), clock,
            NullLogger<GenerateDeviceLinkCodeCommandHandler>.Instance);

        var result = await handler.Handle(new GenerateDeviceLinkCodeCommand(userId), default);

        result.Code.Should().NotBeNullOrEmpty();
        inMemory.DeviceLinkCodes.Where(c => c.UserId == userId).Should().HaveCount(1);
    }

    [Fact]
    public async Task Generate_gives_up_after_max_retries()
    {
        using var inMemory = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        // Falla siempre — más allá del máximo de reintentos del handler (5).
        var db = new ThrowingDbContextDecorator(inMemory, failFirstNCalls: 999);

        var handler = new GenerateDeviceLinkCodeCommandHandler(
            db, new SequentialTokenGenerator(), new FakeDeviceLinkCodeHasher(),
            new ControllableViolationDetector(alwaysReturn: true), clock,
            NullLogger<GenerateDeviceLinkCodeCommandHandler>.Instance);

        var act = () => handler.Handle(new GenerateDeviceLinkCodeCommand(userId), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
