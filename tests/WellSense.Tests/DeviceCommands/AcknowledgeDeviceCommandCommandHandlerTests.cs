using FluentAssertions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Devices.Commands.AcknowledgeDeviceCommand;
using WellSense.Domain.Devices;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.DeviceCommands;

public class AcknowledgeDeviceCommandCommandHandlerTests
{
    private static (Device device, DeviceCommand command) SeedDeviceWithCommand(
        WellSense.Infrastructure.Persistence.WellSenseDbContext db, Guid userId, FixedClock clock, DeviceCommandStatus status = DeviceCommandStatus.Delivered)
    {
        var device = new Device { Id = Guid.NewGuid(), UserId = userId, Type = DeviceType.Watch, PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Devices.Add(device);
        var command = new DeviceCommand
        {
            Id = Guid.NewGuid(), DeviceId = device.Id, UserId = userId, Type = DeviceCommandType.StartMonitoring,
            Status = status, CreatedAt = clock.UtcNow, DeliveredAt = status != DeviceCommandStatus.Pending ? clock.UtcNow : null,
            ExpiresAt = clock.UtcNow.AddHours(24)
        };
        db.DeviceCommands.Add(command);
        db.SaveChangesAsync().GetAwaiter().GetResult();
        return (device, command);
    }

    [Fact]
    public async Task Acknowledging_sets_status_and_publishes_the_event()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var (device, command) = SeedDeviceWithCommand(db, userId, clock);
        var publisher = new SpyPublisherForDeviceCommands();
        var handler = new AcknowledgeDeviceCommandCommandHandler(db, publisher, clock);

        await handler.Handle(new AcknowledgeDeviceCommandCommand(userId, device.Id, command.Id, "ACKNOWLEDGED", "{\"ok\":true}"), default);

        var updated = db.DeviceCommands.Single();
        updated.Status.Should().Be(DeviceCommandStatus.Acknowledged);
        updated.AcknowledgedAt.Should().NotBeNull();
        updated.AckPayload.Should().Be("{\"ok\":true}");
        publisher.Published.Should().ContainSingle();
    }

    [Fact]
    public async Task Acknowledging_with_failed_status_marks_the_command_as_failed()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var (device, command) = SeedDeviceWithCommand(db, userId, clock);
        var handler = new AcknowledgeDeviceCommandCommandHandler(db, new SpyPublisherForDeviceCommands(), clock);

        await handler.Handle(new AcknowledgeDeviceCommandCommand(userId, device.Id, command.Id, "FAILED", null), default);

        db.DeviceCommands.Single().Status.Should().Be(DeviceCommandStatus.Failed);
    }

    [Fact]
    public async Task Acknowledging_an_already_terminal_command_is_idempotent()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var (device, command) = SeedDeviceWithCommand(db, userId, clock, DeviceCommandStatus.Acknowledged);
        command.AcknowledgedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        var firstAckAt = command.AcknowledgedAt;
        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        var handler = new AcknowledgeDeviceCommandCommandHandler(db, new SpyPublisherForDeviceCommands(), clock);

        await handler.Handle(new AcknowledgeDeviceCommandCommand(userId, device.Id, command.Id, "ACKNOWLEDGED", null), default);

        db.DeviceCommands.Single().AcknowledgedAt.Should().Be(firstAckAt); // no se movió — no se reprocesó
    }

    [Fact]
    public async Task Acknowledging_a_command_that_does_not_exist_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = new Device { Id = Guid.NewGuid(), UserId = userId, Type = DeviceType.Watch, PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        var handler = new AcknowledgeDeviceCommandCommandHandler(db, new SpyPublisherForDeviceCommands(), clock);

        var act = () => handler.Handle(new AcknowledgeDeviceCommandCommand(userId, device.Id, Guid.NewGuid(), "ACKNOWLEDGED", null), default);

        await act.Should().ThrowAsync<SyncDomainException>().Where(e => e.ErrorCode == "COMMAND_NOT_FOUND");
    }
}

file class SpyPublisherForDeviceCommands : MediatR.IPublisher
{
    public List<object> Published { get; } = [];
    public Task Publish(object notification, CancellationToken ct = default) { Published.Add(notification); return Task.CompletedTask; }
    public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : MediatR.INotification
    { Published.Add(notification!); return Task.CompletedTask; }
}
