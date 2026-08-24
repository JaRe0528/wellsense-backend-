using FluentAssertions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Devices.Commands.ListDeviceCommands;
using WellSense.Application.Devices.Commands.ListPendingDeviceCommands;
using WellSense.Domain.Devices;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.DeviceCommands;

public class ListDeviceCommandsTests
{
    private static Device SeedDevice(WellSense.Infrastructure.Persistence.WellSenseDbContext db, Guid userId, FixedClock clock)
    {
        var device = new Device { Id = Guid.NewGuid(), UserId = userId, Type = DeviceType.Watch, PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Devices.Add(device);
        db.SaveChangesAsync().GetAwaiter().GetResult();
        return device;
    }

    [Fact]
    public async Task ListDeviceCommands_returns_full_history_ordered_newest_first()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedDevice(db, userId, clock);
        db.DeviceCommands.Add(new DeviceCommand
        {
            Id = Guid.NewGuid(), DeviceId = device.Id, UserId = userId, Type = DeviceCommandType.SyncNow,
            Status = DeviceCommandStatus.Acknowledged, CreatedAt = clock.UtcNow.AddMinutes(-10),
            DeliveredAt = clock.UtcNow.AddMinutes(-9), AcknowledgedAt = clock.UtcNow.AddMinutes(-8), ExpiresAt = clock.UtcNow.AddHours(24)
        });
        db.DeviceCommands.Add(new DeviceCommand
        {
            Id = Guid.NewGuid(), DeviceId = device.Id, UserId = userId, Type = DeviceCommandType.RequestStatus,
            Status = DeviceCommandStatus.Pending, CreatedAt = clock.UtcNow, ExpiresAt = clock.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();

        var handler = new ListDeviceCommandsQueryHandler(db);
        var result = await handler.Handle(new ListDeviceCommandsQuery(userId, device.Id), default);

        result.Should().HaveCount(2);
        result[0].Type.Should().Be("REQUEST_STATUS"); // más reciente primero
        result[1].Status.Should().Be("ACKNOWLEDGED");
    }

    [Fact]
    public async Task ListPendingDeviceCommands_excludes_acknowledged_and_failed()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedDevice(db, userId, clock);
        db.DeviceCommands.Add(new DeviceCommand
        {
            Id = Guid.NewGuid(), DeviceId = device.Id, UserId = userId, Type = DeviceCommandType.StartMonitoring,
            Status = DeviceCommandStatus.Pending, CreatedAt = clock.UtcNow, ExpiresAt = clock.UtcNow.AddHours(24)
        });
        db.DeviceCommands.Add(new DeviceCommand
        {
            Id = Guid.NewGuid(), DeviceId = device.Id, UserId = userId, Type = DeviceCommandType.StopMonitoring,
            Status = DeviceCommandStatus.Acknowledged, CreatedAt = clock.UtcNow, DeliveredAt = clock.UtcNow,
            AcknowledgedAt = clock.UtcNow, ExpiresAt = clock.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();

        var handler = new ListPendingDeviceCommandsQueryHandler(db);
        var result = await handler.Handle(new ListPendingDeviceCommandsQuery(userId, device.Id), default);

        result.Should().ContainSingle(c => c.Type == "START_MONITORING");
    }

    [Fact]
    public async Task Listing_commands_of_a_device_owned_by_another_user_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var device = SeedDevice(db, owner, clock);
        var handler = new ListDeviceCommandsQueryHandler(db);

        var act = () => handler.Handle(new ListDeviceCommandsQuery(attacker, device.Id), default);

        await act.Should().ThrowAsync<SyncDomainException>().Where(e => e.ErrorCode == "DEVICE_NOT_FOUND");
    }
}
