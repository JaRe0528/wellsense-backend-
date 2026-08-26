using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;

namespace WellSense.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.ToTable("devices");
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        // Parte 5 (post-Bloque-10): se agregó WEB — no hay CHECK a nivel de BD que
        // migrar (devices.type siempre fue `text` simple, migración 003, sin
        // restricción de valores ahí); la restricción real vivía en este ternario
        // binario (todo lo que no fuera "PHONE" caía silenciosamente en "WATCH", un bug
        // latente que nunca se manifestó porque el validador ya solo dejaba pasar esos
        // dos valores) y en RegisterDeviceCommandValidator. Se corrige el switch
        // completo acá de una vez, mismo criterio que MeasurementType/DeviceCommandType.
        b.Property(x => x.Type).HasConversion(v => TypeToDb(v), v => TypeFromDb(v));
        b.Property(x => x.Status).HasConversion(v => StatusToDb(v), v => StatusFromDb(v));
    }

    private static string TypeToDb(DeviceType v) => v switch
    {
        DeviceType.Phone => "PHONE",
        DeviceType.Watch => "WATCH",
        DeviceType.Web => "WEB",
        _ => throw new ArgumentOutOfRangeException(nameof(v))
    };

    private static DeviceType TypeFromDb(string v) => v switch
    {
        "PHONE" => DeviceType.Phone,
        "WATCH" => DeviceType.Watch,
        "WEB" => DeviceType.Web,
        _ => throw new ArgumentOutOfRangeException(nameof(v))
    };

    // Ver el mismo comentario en IdentityConfigurations.UserConfiguration: extraer el
    // switch a un método estático con tipo de retorno explícito evita CS8514 cuando se
    // pasa directo como argumento de HasConversion.
    private static string StatusToDb(DeviceStatus v) => v switch
    {
        DeviceStatus.Inactive => "INACTIVE",
        DeviceStatus.Unpaired => "UNPAIRED",
        _ => "ACTIVE"
    };

    private static DeviceStatus StatusFromDb(string v) => v switch
    {
        "INACTIVE" => DeviceStatus.Inactive,
        "UNPAIRED" => DeviceStatus.Unpaired,
        _ => DeviceStatus.Active
    };
}

public class DeviceLinkCodeConfiguration : IEntityTypeConfiguration<DeviceLinkCode>
{
    public void Configure(EntityTypeBuilder<DeviceLinkCode> b)
    {
        b.ToTable("device_link_codes");
        b.HasKey(x => x.Id);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        b.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).IsRequired(false);
        // Los tres índices únicos/parciales (one_active_per_user, active_code_hash,
        // expires_at parcial) se crean en la migración 005 vía SQL crudo — EF Core
        // no traduce índices parciales de forma nativa de manera confiable en todas
        // las versiones, así que se documentan aquí como comentario y se mantienen
        // como fuente de verdad las migraciones, no el modelo Fluent.
    }
}

public class DeviceCommandConfiguration : IEntityTypeConfiguration<DeviceCommand>
{
    public void Configure(EntityTypeBuilder<DeviceCommand> b)
    {
        b.ToTable("device_commands");
        b.HasKey(x => x.Id);
        b.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        b.HasIndex(x => new { x.DeviceId, x.Status }); // ix_device_commands_device_status
        b.HasIndex(x => new { x.UserId, x.CreatedAt }); // ix_device_commands_user_created
        b.Property(x => x.Payload).HasColumnType("jsonb");
        b.Property(x => x.AckPayload).HasColumnType("jsonb");

        // Type tiene guion bajo en la BD (START_MONITORING) — no coincide con el nombre
        // del enum de C# (StartMonitoring) ni con un simple ToUpperInvariant(), hace
        // falta el switch completo (mismo patrón que MeasurementType, Bloque 4).
        b.Property(x => x.Type).HasConversion(v => TypeToDb(v), v => TypeFromDb(v));

        // Status SÍ coincide con ToUpperInvariant() (nombres de una sola palabra, sin
        // guion bajo) — mismo patrón simple que MembershipPlan.Code/Subscription.Status/
        // Payment.Status (Bloque 6). Aprendido de los 4 bugs anteriores de
        // HasConversion<string>() genérico: nunca usarlo aquí sin verificar primero que
        // el nombre del enum coincide EXACTAMENTE con el literal del CHECK salvo mayúsculas.
        b.Property(x => x.Status).HasConversion(
            v => v.ToString().ToUpperInvariant(),
            v => (DeviceCommandStatus)Enum.Parse(typeof(DeviceCommandStatus), v, true));
    }

    private static string TypeToDb(DeviceCommandType v) => v switch
    {
        DeviceCommandType.StartMonitoring => "START_MONITORING",
        DeviceCommandType.StopMonitoring => "STOP_MONITORING",
        DeviceCommandType.ChangeInterval => "CHANGE_INTERVAL",
        DeviceCommandType.SyncNow => "SYNC_NOW",
        DeviceCommandType.RequestStatus => "REQUEST_STATUS",
        _ => throw new ArgumentOutOfRangeException(nameof(v))
    };

    private static DeviceCommandType TypeFromDb(string v) => v switch
    {
        "START_MONITORING" => DeviceCommandType.StartMonitoring,
        "STOP_MONITORING" => DeviceCommandType.StopMonitoring,
        "CHANGE_INTERVAL" => DeviceCommandType.ChangeInterval,
        "SYNC_NOW" => DeviceCommandType.SyncNow,
        "REQUEST_STATUS" => DeviceCommandType.RequestStatus,
        _ => throw new ArgumentOutOfRangeException(nameof(v))
    };
}
