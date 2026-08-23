using FluentAssertions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Sync.SyncMeasurements;
using WellSense.Domain.Devices;
using WellSense.Domain.Measurements;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Sync;

public class SyncMeasurementsCommandHandlerTests
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
    public async Task First_sync_accepts_all_valid_measurements()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(db, userId, clock);
        var handler = new SyncMeasurementsCommandHandler(db, new ControllableViolationDetector(alwaysReturn: false), clock);

        var items = new List<MeasurementItem>
        {
            new(Guid.NewGuid(), "HEART_RATE", 72, "bpm", clock.UtcNow.AddMinutes(-10)),
            new(Guid.NewGuid(), "STEPS", 500, "steps", clock.UtcNow.AddMinutes(-5))
        };

        var result = await handler.Handle(new SyncMeasurementsCommand(userId, device.Id, "req-1", items), default);

        result.Status.Should().Be("COMPLETED");
        result.AcceptedCount.Should().Be(2);
        result.DuplicatedCount.Should().Be(0);
        result.RejectedCount.Should().Be(0);
        db.Measurements.Should().HaveCount(2);
    }

    [Fact]
    public async Task Retrying_the_same_request_id_is_idempotent_and_does_not_duplicate()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(db, userId, clock);
        var handler = new SyncMeasurementsCommandHandler(db, new ControllableViolationDetector(alwaysReturn: false), clock);

        var items = new List<MeasurementItem> { new(Guid.NewGuid(), "HEART_RATE", 72, "bpm", clock.UtcNow.AddMinutes(-10)) };
        var command = new SyncMeasurementsCommand(userId, device.Id, "same-batch-id", items);

        var first = await handler.Handle(command, default);
        var second = await handler.Handle(command, default); // el cliente reintenta la misma llamada

        first.AcceptedCount.Should().Be(1);
        second.AcceptedCount.Should().Be(1); // mismo resultado devuelto, no reprocesado
        db.Measurements.Should().HaveCount(1); // nunca se duplicó la medición
        db.SyncOperations.Should().HaveCount(1); // nunca se creó una segunda sync_operation
    }

    [Fact]
    public async Task Same_event_id_in_a_different_batch_counts_as_duplicated_not_accepted()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(db, userId, clock);
        var handler = new SyncMeasurementsCommandHandler(db, new ControllableViolationDetector(alwaysReturn: false), clock);

        var eventId = Guid.NewGuid();
        var recordedAt = clock.UtcNow.AddMinutes(-10);

        await handler.Handle(new SyncMeasurementsCommand(
            userId, device.Id, "batch-1", [new MeasurementItem(eventId, "HEART_RATE", 72, "bpm", recordedAt)]), default);

        // Mismo evento (mismo id + mismo recordedAt), pero un batch DISTINTO (requestId
        // distinto) — simula que el batch anterior se recibió parcialmente y el cliente
        // reintentó incluyendo lecturas que ya se habían guardado.
        var secondResult = await handler.Handle(new SyncMeasurementsCommand(
            userId, device.Id, "batch-2", [new MeasurementItem(eventId, "HEART_RATE", 72, "bpm", recordedAt)]), default);

        secondResult.AcceptedCount.Should().Be(0);
        secondResult.DuplicatedCount.Should().Be(1);
        db.Measurements.Should().HaveCount(1); // no se insertó una segunda fila
    }

    [Fact]
    public async Task Invalid_type_is_rejected_without_blocking_the_rest_of_the_batch()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(db, userId, clock);
        var handler = new SyncMeasurementsCommandHandler(db, new ControllableViolationDetector(alwaysReturn: false), clock);

        var items = new List<MeasurementItem>
        {
            new(Guid.NewGuid(), "NOT_A_REAL_TYPE", 1, "x", clock.UtcNow.AddMinutes(-1)),
            new(Guid.NewGuid(), "STEPS", 100, "steps", clock.UtcNow.AddMinutes(-1))
        };

        var result = await handler.Handle(new SyncMeasurementsCommand(userId, device.Id, "req-2", items), default);

        result.AcceptedCount.Should().Be(1);
        result.RejectedCount.Should().Be(1);
        result.RejectedItems.Should().ContainSingle(r => r.Reason == "INVALID_TYPE");
    }

    [Fact]
    public async Task Future_recorded_at_beyond_clock_skew_is_rejected()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(db, userId, clock);
        var handler = new SyncMeasurementsCommandHandler(db, new ControllableViolationDetector(alwaysReturn: false), clock);

        var items = new List<MeasurementItem>
        {
            new(Guid.NewGuid(), "STEPS", 100, "steps", clock.UtcNow.AddHours(1))
        };

        var result = await handler.Handle(new SyncMeasurementsCommand(userId, device.Id, "req-3", items), default);

        result.RejectedItems.Should().ContainSingle(r => r.Reason == "RECORDED_AT_IN_FUTURE");
    }

    [Fact]
    public async Task Syncing_to_a_device_of_another_user_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var device = SeedActiveDevice(db, owner, clock);
        var handler = new SyncMeasurementsCommandHandler(db, new ControllableViolationDetector(alwaysReturn: false), clock);

        var act = () => handler.Handle(new SyncMeasurementsCommand(
            attacker, device.Id, "req-4", [new MeasurementItem(Guid.NewGuid(), "STEPS", 1, "steps", clock.UtcNow)]), default);

        await act.Should().ThrowAsync<SyncDomainException>().Where(e => e.ErrorCode == "DEVICE_NOT_FOUND");
    }

    [Fact]
    public async Task Syncing_to_an_unpaired_device_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(db, userId, clock);
        device.Status = DeviceStatus.Unpaired;
        await db.SaveChangesAsync();
        var handler = new SyncMeasurementsCommandHandler(db, new ControllableViolationDetector(alwaysReturn: false), clock);

        var act = () => handler.Handle(new SyncMeasurementsCommand(
            userId, device.Id, "req-5", [new MeasurementItem(Guid.NewGuid(), "STEPS", 1, "steps", clock.UtcNow)]), default);

        await act.Should().ThrowAsync<SyncDomainException>().Where(e => e.ErrorCode == "DEVICE_NOT_FOUND");
    }

    [Fact]
    public async Task Concurrent_race_on_same_request_id_returns_the_winning_result_instead_of_throwing()
    {
        var sharedDbName = $"race-test-{Guid.NewGuid()}";
        using var inMemory = InMemoryDbContextFactory.Create(sharedDbName);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();
        var device = SeedActiveDevice(inMemory, userId, clock);

        // No se pre-inserta la fila "ganadora" antes de llamar al handler: eso haría
        // que el propio chequeo de replay idempotente del handler (su primer SELECT)
        // la encontrara y nunca llegara al bloque de manejo de la carrera, invalidando
        // la prueba. En su lugar, el decorador simula la carrera real: el SELECT inicial
        // del handler no encuentra nada (porque, en este instante, todavía no existe),
        // y es recién CUANDO el handler intenta guardar que "aparece" la fila ganadora
        // — exactamente como se vería la carrera desde la perspectiva del handler.
        var winningOperation = new SyncOperation
        {
            Id = Guid.NewGuid(), DeviceId = device.Id, RequestId = "raced-request",
            Status = SyncStatus.Completed, AcceptedCount = 7, DuplicatedCount = 0, RejectedCount = 0,
            CreatedAt = clock.UtcNow
        };
        var throwingDb = new ThrowingDbContextDecoratorWithSideEffect(inMemory, () =>
        {
            // Crítico: esto NO debe usar `inMemory` (la misma instancia/DbContext que ya
            // está usando el handler bajo prueba) — el proveedor InMemory de EF permite
            // que varios DbContext independientes compartan el mismo store con solo
            // apuntar al mismo nombre de base (`sharedDbName`). Si en cambio se llamara
            // `inMemory.SaveChanges()` aquí, ese SaveChanges confirmaría de paso TODO lo
            // que el handler ya había agregado a SU PROPIO change tracker en este mismo
            // Handle() (su propia SyncOperation "perdedora", con AcceptedCount=1) — no
            // solo la fila ganadora. El resultado quedaría contaminado por cuál de las
            // dos filas encuentra primero el FirstOrDefaultAsync sin ORDER BY, en vez de
            // reflejar de verdad "otro proceso, aislado, que ganó la carrera".
            using var otherProcessDb = InMemoryDbContextFactory.Create(sharedDbName);
            otherProcessDb.SyncOperations.Add(winningOperation);
            otherProcessDb.SaveChanges();
        });

        var handler = new SyncMeasurementsCommandHandler(throwingDb, new ControllableViolationDetector(alwaysReturn: true), clock);

        var result = await handler.Handle(new SyncMeasurementsCommand(
            userId, device.Id, "raced-request", [new MeasurementItem(Guid.NewGuid(), "STEPS", 1, "steps", clock.UtcNow)]), default);

        result.AcceptedCount.Should().Be(7); // el resultado de la request que SÍ ganó, no la perdedora (1) ni un error
        inMemory.SyncOperations.Should().HaveCount(1); // nunca se insertó una segunda fila — la "perdedora" del handler nunca se confirmó
    }
}

/// <summary>
/// Decorador de un solo uso para esta prueba: en la ÚNICA llamada a SaveChangesAsync,
/// primero ejecuta el efecto secundario (que simula a "otro proceso" confirmando la fila
/// ganadora justo en ese instante) y luego lanza la excepción simulada — así el handler
/// bajo prueba ve exactamente lo que vería en una carrera real: su propio guardado falla
/// DESPUÉS de que la fila competidora ya exista.
/// </summary>
file class ThrowingDbContextDecoratorWithSideEffect(
    WellSense.Infrastructure.Persistence.WellSenseDbContext inner, Action sideEffectBeforeThrow)
    : WellSense.Application.Common.Interfaces.IWellSenseDbContext
{
    private bool _thrown;

    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Identity.User> Users => inner.Users;
    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Identity.RefreshToken> RefreshTokens => inner.RefreshTokens;
    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Identity.EmailVerificationToken> EmailVerificationTokens => inner.EmailVerificationTokens;
    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Identity.PasswordResetToken> PasswordResetTokens => inner.PasswordResetTokens;
    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Identity.AuditLog> AuditLogs => inner.AuditLogs;
    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Devices.Device> Devices => inner.Devices;
    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Devices.DeviceLinkCode> DeviceLinkCodes => inner.DeviceLinkCodes;
    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Profiles.Profile> Profiles => inner.Profiles;
    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Profiles.Goal> Goals => inner.Goals;
    public Microsoft.EntityFrameworkCore.DbSet<WellSense.Domain.Profiles.OnboardingSurvey> OnboardingSurveys => inner.OnboardingSurveys;
    public Microsoft.EntityFrameworkCore.DbSet<Measurement> Measurements => inner.Measurements;
    public Microsoft.EntityFrameworkCore.DbSet<SyncOperation> SyncOperations => inner.SyncOperations;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (!_thrown)
        {
            _thrown = true;
            sideEffectBeforeThrow();
            throw new Microsoft.EntityFrameworkCore.DbUpdateException(
                "simulated unique violation", new InvalidOperationException("simulated"));
        }
        return inner.SaveChangesAsync(cancellationToken);
    }

    public Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class
        => inner.Entry(entity);
}
