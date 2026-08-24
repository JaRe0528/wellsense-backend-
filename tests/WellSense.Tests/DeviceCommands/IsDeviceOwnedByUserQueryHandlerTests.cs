using FluentAssertions;
using WellSense.Application.Devices.IsDeviceOwnedByUser;
using WellSense.Domain.Devices;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.DeviceCommands;

public class IsDeviceOwnedByUserQueryHandlerTests
{
    [Fact]
    public async Task Returns_true_for_the_owner_and_false_for_anyone_else()
    {
        using var db = InMemoryDbContextFactory.Create();
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var device = new Device { Id = Guid.NewGuid(), UserId = owner, Type = DeviceType.Watch, PairedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        var handler = new IsDeviceOwnedByUserQueryHandler(db);

        (await handler.Handle(new IsDeviceOwnedByUserQuery(owner, device.Id), default)).Should().BeTrue();
        (await handler.Handle(new IsDeviceOwnedByUserQuery(attacker, device.Id), default)).Should().BeFalse();
        (await handler.Handle(new IsDeviceOwnedByUserQuery(owner, Guid.NewGuid()), default)).Should().BeFalse();
    }
}
