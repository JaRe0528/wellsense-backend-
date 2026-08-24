using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Devices.Commands.IssueDeviceCommand;
using WellSense.Domain.Devices;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.DeviceCommands;

public class IssueDeviceCommandCommandHandlerTests
{
    private static Device SeedActiveDevice(WellSense.Infrastructure.Persistence.WellSenseDbContext db, Guid userId, FixedClock clock)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(), UserId = userId, Type = DeviceType.Watch, Status = DeviceStatus.Active,
            PairedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        db.Devices.Add(device);
        db.SaveChangesAsync().GetAwaiter().GetResult();
        return device;
    }

    [Fact]
    public async Task Issuing_a_command_creates_a_pending_row_that_becomes_delivered_when_the_push_succeeds()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(db, userId, clock);
        var notifier = new SpyDeviceCommandNotifier();
        var handler = new IssueDeviceCommandCommandHandler(db, notifier, clock, NullLogger<IssueDeviceCommandCommandHandler>.Instance);

        var result = await handler.Handle(new IssueDeviceCommandCommand(userId, device.Id, "START_MONITORING", null), default);

        result.Status.Should().Be("DELIVERED"); // el push (falso, siempre exitoso) se intentó y "funcionó"
        notifier.Calls.Should().ContainSingle(c => c.DeviceId == device.Id);
        db.DeviceCommands.Single().Status.Should().Be(DeviceCommandStatus.Delivered);
        db.DeviceCommands.Single().DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Command_stays_pending_when_the_push_fails_but_is_still_created()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(db, userId, clock);
        var notifier = new SpyDeviceCommandNotifier { ThrowOnNotify = true };
        var handler = new IssueDeviceCommandCommandHandler(db, notifier, clock, NullLogger<IssueDeviceCommandCommandHandler>.Instance);

        var result = await handler.Handle(new IssueDeviceCommandCommand(userId, device.Id, "SYNC_NOW", null), default);

        // El fallo del push NUNCA debe tumbar la emisión del comando — sigue existiendo,
        // solo que en PENDING en vez de DELIVERED, recuperable después vía polling.
        result.Status.Should().Be("PENDING");
        db.DeviceCommands.Should().ContainSingle();
    }

    [Fact]
    public async Task Change_interval_requires_a_valid_payload_before_reaching_the_handler()
    {
        // La validación de forma vive en el FluentValidation validator, no en el
        // handler — esta prueba confirma que el propio validador (no el handler)
        // rechaza un CHANGE_INTERVAL sin intervalSeconds.
        var validator = new IssueDeviceCommandCommandValidator();

        var withoutPayload = await validator.ValidateAsync(
            new IssueDeviceCommandCommand(Guid.NewGuid(), Guid.NewGuid(), "CHANGE_INTERVAL", null));
        var withValidPayload = await validator.ValidateAsync(
            new IssueDeviceCommandCommand(Guid.NewGuid(), Guid.NewGuid(), "CHANGE_INTERVAL", "{\"intervalSeconds\":30}"));

        withoutPayload.IsValid.Should().BeFalse();
        withValidPayload.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Issuing_to_a_device_of_another_user_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var device = SeedActiveDevice(db, owner, clock);
        var handler = new IssueDeviceCommandCommandHandler(db, new SpyDeviceCommandNotifier(), clock, NullLogger<IssueDeviceCommandCommandHandler>.Instance);

        var act = () => handler.Handle(new IssueDeviceCommandCommand(attacker, device.Id, "SYNC_NOW", null), default);

        await act.Should().ThrowAsync<SyncDomainException>().Where(e => e.ErrorCode == "DEVICE_NOT_FOUND");
    }

    [Fact]
    public async Task Issuing_to_an_unpaired_device_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(db, userId, clock);
        device.Status = DeviceStatus.Unpaired;
        await db.SaveChangesAsync();
        var handler = new IssueDeviceCommandCommandHandler(db, new SpyDeviceCommandNotifier(), clock, NullLogger<IssueDeviceCommandCommandHandler>.Instance);

        var act = () => handler.Handle(new IssueDeviceCommandCommand(userId, device.Id, "SYNC_NOW", null), default);

        await act.Should().ThrowAsync<SyncDomainException>().Where(e => e.ErrorCode == "DEVICE_NOT_FOUND");
    }
}
