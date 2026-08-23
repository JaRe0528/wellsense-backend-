using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WellSense.Domain.Measurements;
using WellSense.Domain.Wellness;
using WellSense.Domain.SelfReports;
using WellSense.Domain.Notifications;
using WellSense.Domain.Billing;

namespace WellSense.Infrastructure.Persistence.Configurations;

// Los módulos de negocio de estas entidades (Measurements, Sync, ML, Notifications,
// Memberships/Payments) se implementan en bloques posteriores. Aquí solo se mapea
// lo mínimo para que el modelo de EF sea coherente con el esquema ya migrado
// (001-013). No se exponen repositorios/casos de uso todavía.

public class MeasurementConfiguration : IEntityTypeConfiguration<Measurement>
{
    public void Configure(EntityTypeBuilder<Measurement> b)
    {
        b.ToTable("measurements");
        b.HasKey(x => new { x.Id, x.RecordedAt }); // PK compuesta: partition key debe integrar toda PK
        // NUNCA HasConversion<string>() genérico aquí: eso serializa con Enum.ToString()
        // ("HeartRate"), que no coincide con el CHECK de la BD ('HEART_RATE') y haría
        // fallar TODO insert. Debe ser el mismo vocabulario que MeasurementTypeExtensions
        // (Domain), aunque a propósito no comparten código entre sí.
        b.Property(x => x.Type).HasConversion(v => TypeToDb(v), v => TypeFromDb(v));
    }

    private static string TypeToDb(MeasurementType v) => v switch
    {
        MeasurementType.HeartRate => "HEART_RATE",
        MeasurementType.Steps => "STEPS",
        MeasurementType.Spo2 => "SPO2",
        MeasurementType.Calories => "CALORIES",
        MeasurementType.SkinTemp => "SKIN_TEMP",
        _ => throw new ArgumentOutOfRangeException(nameof(v))
    };

    private static MeasurementType TypeFromDb(string v) => v switch
    {
        "HEART_RATE" => MeasurementType.HeartRate,
        "STEPS" => MeasurementType.Steps,
        "SPO2" => MeasurementType.Spo2,
        "CALORIES" => MeasurementType.Calories,
        "SKIN_TEMP" => MeasurementType.SkinTemp,
        _ => throw new ArgumentOutOfRangeException(nameof(v))
    };
}

public class SleepSessionConfiguration : IEntityTypeConfiguration<SleepSession>
{
    public void Configure(EntityTypeBuilder<SleepSession> b)
    {
        b.ToTable("sleep_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.DurationMinutes)
            .HasColumnName("duration_minutes")
            .ValueGeneratedOnAddOrUpdate()
            .Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
        b.Property(x => x.Stages).HasColumnType("jsonb");
    }
}

public class ActivitySessionConfiguration : IEntityTypeConfiguration<ActivitySession>
{
    public void Configure(EntityTypeBuilder<ActivitySession> b)
    {
        b.ToTable("activity_sessions");
        b.HasKey(x => x.Id);
    }
}

public class SyncOperationConfiguration : IEntityTypeConfiguration<SyncOperation>
{
    public void Configure(EntityTypeBuilder<SyncOperation> b)
    {
        b.ToTable("sync_operations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.DeviceId, x.RequestId }).IsUnique(); // ux_sync_operations_device_request
        // Mismo motivo que MeasurementConfiguration.Type: HasConversion<string>() genérico
        // daría "Processing"/"Completed"/"Failed", no 'PROCESSING'/'COMPLETED'/'FAILED'
        // (el CHECK de la BD es sensible a mayúsculas).
        b.Property(x => x.Status).HasConversion(v => StatusToDb(v), v => StatusFromDb(v));
    }

    private static string StatusToDb(SyncStatus v) => v switch
    {
        SyncStatus.Completed => "COMPLETED",
        SyncStatus.Failed => "FAILED",
        _ => "PROCESSING"
    };

    private static SyncStatus StatusFromDb(string v) => v switch
    {
        "COMPLETED" => SyncStatus.Completed,
        "FAILED" => SyncStatus.Failed,
        _ => SyncStatus.Processing
    };
}

public class WellnessScoreConfiguration : IEntityTypeConfiguration<WellnessScore>
{
    public void Configure(EntityTypeBuilder<WellnessScore> b)
    {
        b.ToTable("wellness_scores");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
    }
}

public class StressScoreConfiguration : IEntityTypeConfiguration<StressScore>
{
    public void Configure(EntityTypeBuilder<StressScore> b)
    {
        b.ToTable("stress_scores");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.Date }).IsUnique();
        b.Property(x => x.Level).HasConversion<string>();
        b.Property(x => x.Factors).HasColumnType("jsonb");
    }
}

public class MlPredictionConfiguration : IEntityTypeConfiguration<MlPrediction>
{
    public void Configure(EntityTypeBuilder<MlPrediction> b)
    {
        b.ToTable("ml_predictions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Input).HasColumnType("jsonb");
        b.Property(x => x.Output).HasColumnType("jsonb");
    }
}

public class SelfReportConfiguration : IEntityTypeConfiguration<SelfReport>
{
    public void Configure(EntityTypeBuilder<SelfReport> b)
    {
        b.ToTable("self_reports");
        b.HasKey(x => x.Id);
    }
}

public class BreathingSessionConfiguration : IEntityTypeConfiguration<BreathingSession>
{
    public void Configure(EntityTypeBuilder<BreathingSession> b)
    {
        b.ToTable("breathing_sessions");
        b.HasKey(x => x.Id);
    }
}

public class ExperimentConfiguration : IEntityTypeConfiguration<Experiment>
{
    public void Configure(EntityTypeBuilder<Experiment> b)
    {
        b.ToTable("experiments");
        b.HasKey(x => x.Id);
        b.Property(x => x.BaselineMetric).HasColumnType("jsonb");
        b.Property(x => x.ResultMetric).HasColumnType("jsonb");
    }
}

public class NotificationTokenConfiguration : IEntityTypeConfiguration<NotificationToken>
{
    public void Configure(EntityTypeBuilder<NotificationToken> b)
    {
        b.ToTable("notification_tokens");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.DeviceId, x.FcmToken }).IsUnique();
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
    }
}

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> b)
    {
        b.ToTable("reminders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>();
    }
}

public class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    public void Configure(EntityTypeBuilder<MembershipPlan> b)
    {
        b.ToTable("membership_plans");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Code).HasConversion(
            v => v.ToString().ToUpperInvariant(),
            v => (PlanCode)Enum.Parse(typeof(PlanCode), v, true));
        b.Property(x => x.Features).HasColumnType("jsonb");
    }
}

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.ToTable("subscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>();
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.TransactionId).IsUnique();
        b.Property(x => x.Status).HasConversion<string>();
    }
}
