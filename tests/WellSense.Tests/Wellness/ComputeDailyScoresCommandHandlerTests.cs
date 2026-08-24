using FluentAssertions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Wellness.ComputeDailyScores;
using WellSense.Domain.Measurements;
using WellSense.Domain.Profiles;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Wellness;

public class ComputeDailyScoresCommandHandlerTests
{
    private const string MexicoCity = "America/Mexico_City";

    [Fact]
    public async Task No_data_at_all_throws_insufficient_data()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        var handler = new ComputeDailyScoresCommandHandler(db, clock);

        var act = () => handler.Handle(new ComputeDailyScoresCommand(Guid.NewGuid(), new DateOnly(2026, 8, 23)), default);

        await act.Should().ThrowAsync<MlDomainException>().Where(e => e.ErrorCode == "INSUFFICIENT_DATA");
    }

    [Fact]
    public async Task Only_steps_computes_wellness_but_not_stress()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        var userId = Guid.NewGuid();
        var deviceId = SeedDevice(db, userId);
        AddMeasurement(db, userId, deviceId, MeasurementType.Steps, 5000, new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        await db.SaveChangesAsync();

        var handler = new ComputeDailyScoresCommandHandler(db, clock);
        var result = await handler.Handle(new ComputeDailyScoresCommand(userId, new DateOnly(2026, 8, 23)), default);

        result.Wellness.Should().NotBeNull();
        result.Wellness!.Score.Should().Be(50m); // 5000 pasos = componente de actividad 50, único componente disponible
        result.Stress.Should().BeNull(); // sin frecuencia cardíaca ni sueño, no hay nada de qué calcular estrés
    }

    [Fact]
    public async Task Steps_sleep_and_heart_rate_compute_both_scores_with_full_confidence()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        var userId = Guid.NewGuid();
        var deviceId = SeedDevice(db, userId);

        AddMeasurement(db, userId, deviceId, MeasurementType.Steps, 8000, new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        AddMeasurement(db, userId, deviceId, MeasurementType.HeartRate, 65, new DateTimeOffset(2026, 8, 23, 11, 0, 0, TimeSpan.Zero));
        db.SleepSessions.Add(new SleepSession
        {
            Id = Guid.NewGuid(), UserId = userId,
            StartAt = new DateTimeOffset(2026, 8, 22, 23, 0, 0, TimeSpan.Zero),
            EndAt = new DateTimeOffset(2026, 8, 23, 7, 0, 0, TimeSpan.Zero), // termina "hoy" — cuenta para el 23
            CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new ComputeDailyScoresCommandHandler(db, clock);
        var result = await handler.Handle(new ComputeDailyScoresCommand(userId, new DateOnly(2026, 8, 23)), default);

        result.Wellness.Should().NotBeNull();
        result.Stress.Should().NotBeNull();
        result.Stress!.Confidence.Should().Be(1.0m); // ambos componentes de estrés disponibles (HR + sueño)
        db.MlPredictions.Should().ContainSingle(p => p.UserId == userId && p.ModelVersion == "rules-v1");
    }

    [Fact]
    public async Task Recomputing_the_same_day_updates_in_place_instead_of_duplicating()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero));
        var userId = Guid.NewGuid();
        var deviceId = SeedDevice(db, userId);
        AddMeasurement(db, userId, deviceId, MeasurementType.Steps, 3000, new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero));
        await db.SaveChangesAsync();
        var handler = new ComputeDailyScoresCommandHandler(db, clock);

        await handler.Handle(new ComputeDailyScoresCommand(userId, new DateOnly(2026, 8, 23)), default);

        // Llega más data después (sync tardío) y se recalcula el mismo día.
        AddMeasurement(db, userId, deviceId, MeasurementType.Steps, 7000, new DateTimeOffset(2026, 8, 23, 18, 0, 0, TimeSpan.Zero));
        await db.SaveChangesAsync();
        var secondResult = await handler.Handle(new ComputeDailyScoresCommand(userId, new DateOnly(2026, 8, 23)), default);

        db.WellnessScores.Should().HaveCount(1); // se actualizó la misma fila, no se duplicó
        secondResult.Wellness!.Score.Should().Be(100m); // 3000+7000 = 10000 pasos = componente de actividad 100
    }

    [Fact]
    public async Task Uses_the_users_local_timezone_not_utc_to_attribute_a_measurement_to_the_correct_day()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 24, 4, 0, 0, TimeSpan.Zero)); // 22:00 del 23 en CDMX
        var userId = Guid.NewGuid();
        var deviceId = SeedDevice(db, userId);
        db.Profiles.Add(new Profile { Id = Guid.NewGuid(), UserId = userId, Timezone = MexicoCity, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow });

        // Medición tomada a las 22:30 hora CDMX del 23, que en UTC ya es 04:30 del 24 —
        // sin la conversión de zona horaria, un cálculo por fecha UTC la atribuiría al 24.
        AddMeasurement(db, userId, deviceId, MeasurementType.Steps, 4000, new DateTimeOffset(2026, 8, 24, 4, 30, 0, TimeSpan.Zero));
        await db.SaveChangesAsync();

        var handler = new ComputeDailyScoresCommandHandler(db, clock);
        // Sin fecha explícita: debe resolver "hoy" como 23 de agosto en CDMX, no 24 en UTC.
        var result = await handler.Handle(new ComputeDailyScoresCommand(userId, null), default);

        result.Date.Should().Be(new DateOnly(2026, 8, 23));
        result.Wellness.Should().NotBeNull(); // encontró la medición del 23 local
    }

    private static Guid SeedDevice(WellSense.Infrastructure.Persistence.WellSenseDbContext db, Guid userId)
    {
        var device = new WellSense.Domain.Devices.Device
        {
            Id = Guid.NewGuid(), UserId = userId, Type = WellSense.Domain.Devices.DeviceType.Watch,
            PairedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Devices.Add(device);
        db.SaveChanges();
        return device.Id;
    }

    private static void AddMeasurement(
        WellSense.Infrastructure.Persistence.WellSenseDbContext db, Guid userId, Guid deviceId,
        MeasurementType type, decimal value, DateTimeOffset recordedAt)
    {
        db.Measurements.Add(new Measurement
        {
            Id = Guid.NewGuid(), UserId = userId, DeviceId = deviceId, Type = type, Value = value,
            Unit = type == MeasurementType.Steps ? "steps" : "bpm", RecordedAt = recordedAt, CreatedAt = recordedAt
        });
    }
}
