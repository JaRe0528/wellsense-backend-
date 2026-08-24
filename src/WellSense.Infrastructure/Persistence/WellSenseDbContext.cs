using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Billing;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;
using WellSense.Domain.Measurements;
using WellSense.Domain.Notifications;
using WellSense.Domain.Profiles;
using WellSense.Domain.SelfReports;
using WellSense.Domain.Wellness;

namespace WellSense.Infrastructure.Persistence;

public class WellSenseDbContext(DbContextOptions<WellSenseDbContext> options) : DbContext(options), IWellSenseDbContext
{
    // Identidad y acceso
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Perfil
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<OnboardingSurvey> OnboardingSurveys => Set<OnboardingSurvey>();

    // Dispositivos
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceLinkCode> DeviceLinkCodes => Set<DeviceLinkCode>();
    public DbSet<DeviceCommand> DeviceCommands => Set<DeviceCommand>();

    // Mediciones / sync (fuera de alcance funcional del Bloque 1-3, mapeado por completitud del esquema)
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<SleepSession> SleepSessions => Set<SleepSession>();
    public DbSet<ActivitySession> ActivitySessions => Set<ActivitySession>();
    public DbSet<SyncOperation> SyncOperations => Set<SyncOperation>();

    // Bienestar / ML
    public DbSet<WellnessScore> WellnessScores => Set<WellnessScore>();
    public DbSet<StressScore> StressScores => Set<StressScore>();
    public DbSet<MlPrediction> MlPredictions => Set<MlPrediction>();

    // Autoreportes / intervenciones
    public DbSet<SelfReport> SelfReports => Set<SelfReport>();
    public DbSet<BreathingSession> BreathingSessions => Set<BreathingSession>();
    public DbSet<Experiment> Experiments => Set<Experiment>();

    // Notificaciones
    public DbSet<NotificationToken> NotificationTokens => Set<NotificationToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    // Membresías y pagos
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WellSenseDbContext).Assembly);
    }
}
