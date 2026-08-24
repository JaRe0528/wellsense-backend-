using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Billing;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;
using WellSense.Domain.Measurements;
using WellSense.Domain.Notifications;
using WellSense.Domain.Profiles;
using WellSense.Domain.Wellness;
using WellSense.Infrastructure.Persistence;

namespace WellSense.Tests.TestHelpers;

/// <summary>
/// Decorador de IWellSenseDbContext que lanza una DbUpdateException simulada en las
/// primeras N llamadas a SaveChangesAsync, y delega a la instancia real (InMemory) en
/// las siguientes. Permite probar el bucle de reintento de colisión de
/// GenerateDeviceLinkCodeCommandHandler sin depender de un índice único real de Postgres
/// (que el proveedor InMemory de EF no aplica para índices parciales).
/// </summary>
public class ThrowingDbContextDecorator(WellSenseDbContext inner, int failFirstNCalls) : IWellSenseDbContext
{
    private int _calls;

    public DbSet<User> Users => inner.Users;
    public DbSet<RefreshToken> RefreshTokens => inner.RefreshTokens;
    public DbSet<EmailVerificationToken> EmailVerificationTokens => inner.EmailVerificationTokens;
    public DbSet<PasswordResetToken> PasswordResetTokens => inner.PasswordResetTokens;
    public DbSet<AuditLog> AuditLogs => inner.AuditLogs;
    public DbSet<Device> Devices => inner.Devices;
    public DbSet<DeviceLinkCode> DeviceLinkCodes => inner.DeviceLinkCodes;
    public DbSet<Profile> Profiles => inner.Profiles;
    public DbSet<Goal> Goals => inner.Goals;
    public DbSet<OnboardingSurvey> OnboardingSurveys => inner.OnboardingSurveys;
    public DbSet<Measurement> Measurements => inner.Measurements;
    public DbSet<SyncOperation> SyncOperations => inner.SyncOperations;
    public DbSet<NotificationToken> NotificationTokens => inner.NotificationTokens;
    public DbSet<Notification> Notifications => inner.Notifications;
    public DbSet<MembershipPlan> MembershipPlans => inner.MembershipPlans;
    public DbSet<Subscription> Subscriptions => inner.Subscriptions;
    public DbSet<Payment> Payments => inner.Payments;
    public DbSet<SleepSession> SleepSessions => inner.SleepSessions;
    public DbSet<ActivitySession> ActivitySessions => inner.ActivitySessions;
    public DbSet<WellnessScore> WellnessScores => inner.WellnessScores;
    public DbSet<StressScore> StressScores => inner.StressScores;
    public DbSet<MlPrediction> MlPredictions => inner.MlPredictions;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _calls++;
        if (_calls <= failFirstNCalls)
        {
            throw new DbUpdateException("simulated unique violation", new InvalidOperationException("simulated"));
        }
        return inner.SaveChangesAsync(cancellationToken);
    }

    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class => inner.Entry(entity);
}
