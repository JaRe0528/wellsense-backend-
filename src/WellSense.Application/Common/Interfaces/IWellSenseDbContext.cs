using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using WellSense.Domain.Billing;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;
using WellSense.Domain.Measurements;
using WellSense.Domain.Notifications;
using WellSense.Domain.Profiles;
using WellSense.Domain.Wellness;

namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Abstracción del DbContext que puede usar Application sin depender de Npgsql/EF.Design.
/// Expone los DbSets que Auth (Bloque 2), Users+Profile (Bloque 3),
/// Devices+Measurements+Sync (Bloque 4), SignalR+FCM (Bloque 5), Memberships+Payments
/// (Bloque 6), ML V1/reglas (Bloque 7) y Device Command System (Bloque 8) necesitan; el
/// resto de módulos agregarán los suyos cuando les toque su bloque.
/// </summary>
public interface IWellSenseDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<EmailVerificationToken> EmailVerificationTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Device> Devices { get; }
    DbSet<DeviceLinkCode> DeviceLinkCodes { get; }
    DbSet<DeviceCommand> DeviceCommands { get; }
    DbSet<Profile> Profiles { get; }
    DbSet<Goal> Goals { get; }
    DbSet<OnboardingSurvey> OnboardingSurveys { get; }
    DbSet<Measurement> Measurements { get; }
    DbSet<SyncOperation> SyncOperations { get; }
    DbSet<SleepSession> SleepSessions { get; }
    DbSet<ActivitySession> ActivitySessions { get; }
    DbSet<NotificationToken> NotificationTokens { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<MembershipPlan> MembershipPlans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Payment> Payments { get; }
    DbSet<WellnessScore> WellnessScores { get; }
    DbSet<StressScore> StressScores { get; }
    DbSet<MlPrediction> MlPredictions { get; }

    /// <summary>
    /// Una sola llamada = una sola transacción implícita de EF/Postgres. Los handlers
    /// que necesitan atomicidad (ej. invalidar+insertar código de vinculación) agrupan
    /// todos sus cambios en una única llamada a este método en vez de abrir una
    /// transacción explícita — más simple y suficiente para los casos de este bloque.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Expone el change tracker de una entidad puntual — necesario para desprender
    /// (Detach) o revertir el estado de entidades después de un SaveChanges fallido,
    /// ya que EF Core no revierte el change tracker automáticamente cuando la
    /// transacción de BD falla (ver GenerateDeviceLinkCodeCommandHandler).
    /// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
