using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;

namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Abstracción del DbContext que puede usar Application sin depender de Npgsql/EF.Design.
/// Solo expone los DbSets que Auth necesita en este bloque; el resto de módulos
/// agregarán sus propios DbSets a esta interfaz cuando les toque su bloque.
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
