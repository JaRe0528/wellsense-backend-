using FluentAssertions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Devices.ListMyDevices;
using WellSense.Application.Devices.RegisterDevice;
using WellSense.Application.Devices.UnpairDevice;
using WellSense.Application.Devices.UpdateDeviceHeartbeat;
using WellSense.Domain.Devices;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Devices;

public class DevicesTests
{
    [Fact]
    public async Task Register_then_list_shows_the_new_device()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();

        var registerHandler = new RegisterDeviceCommandHandler(db, clock);
        var deviceId = await registerHandler.Handle(
            new RegisterDeviceCommand(userId, "WATCH", "Galaxy Watch 7", "Wear OS 5", "1.2.0"), default);

        var listHandler = new ListMyDevicesQueryHandler(db);
        var devices = await listHandler.Handle(new ListMyDevicesQuery(userId), default);

        devices.Should().ContainSingle(d => d.Id == deviceId && d.Type == "Watch" && d.Status == "Active");
    }

    [Fact]
    public async Task List_only_returns_devices_of_the_current_user()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var registerHandler = new RegisterDeviceCommandHandler(db, clock);

        await registerHandler.Handle(new RegisterDeviceCommand(userA, "PHONE", null, null, null), default);
        await registerHandler.Handle(new RegisterDeviceCommand(userB, "PHONE", null, null, null), default);

        var listHandler = new ListMyDevicesQueryHandler(db);
        var devicesOfA = await listHandler.Handle(new ListMyDevicesQuery(userA), default);

        devicesOfA.Should().HaveCount(1);
    }

    [Fact]
    public async Task Heartbeat_updates_last_seen_and_metadata()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var registerHandler = new RegisterDeviceCommandHandler(db, clock);
        var deviceId = await registerHandler.Handle(new RegisterDeviceCommand(userId, "PHONE", "Pixel 8", "14", "1.0.0"), default);

        clock.UtcNow = clock.UtcNow.AddHours(2);
        var heartbeatHandler = new UpdateDeviceHeartbeatCommandHandler(db, clock);
        await heartbeatHandler.Handle(new UpdateDeviceHeartbeatCommand(userId, deviceId, null, null, "1.1.0"), default);

        var device = db.Devices.Single();
        device.AppVersion.Should().Be("1.1.0");
        device.LastSeenAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task Heartbeat_for_device_of_another_user_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var registerHandler = new RegisterDeviceCommandHandler(db, clock);
        var deviceId = await registerHandler.Handle(new RegisterDeviceCommand(owner, "PHONE", null, null, null), default);

        var heartbeatHandler = new UpdateDeviceHeartbeatCommandHandler(db, clock);
        var act = () => heartbeatHandler.Handle(new UpdateDeviceHeartbeatCommand(attacker, deviceId, null, null, null), default);

        await act.Should().ThrowAsync<SyncDomainException>().Where(e => e.ErrorCode == "DEVICE_NOT_FOUND");
    }

    [Fact]
    public async Task Unpair_sets_status_to_unpaired()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var registerHandler = new RegisterDeviceCommandHandler(db, clock);
        var deviceId = await registerHandler.Handle(new RegisterDeviceCommand(userId, "WATCH", null, null, null), default);

        var unpairHandler = new UnpairDeviceCommandHandler(db, clock);
        await unpairHandler.Handle(new UnpairDeviceCommand(userId, deviceId), default);

        db.Devices.Single().Status.Should().Be(DeviceStatus.Unpaired);
    }

    [Fact]
    public async Task Register_and_unpair_each_write_their_own_audit_log_entry()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var registerHandler = new RegisterDeviceCommandHandler(db, clock);

        var deviceId = await registerHandler.Handle(new RegisterDeviceCommand(userId, "WATCH", null, null, null), default);
        db.AuditLogs.Should().ContainSingle(a => a.UserId == userId && a.Action == "device_registered");

        var unpairHandler = new UnpairDeviceCommandHandler(db, clock);
        await unpairHandler.Handle(new UnpairDeviceCommand(userId, deviceId), default);

        db.AuditLogs.Should().ContainSingle(a => a.Action == "device_unpaired");
        db.AuditLogs.Should().HaveCount(2); // uno de cada acción, no se pisan entre sí
    }
}
