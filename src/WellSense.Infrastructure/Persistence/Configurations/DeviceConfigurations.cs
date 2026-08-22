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
        b.Property(x => x.Type).HasConversion(
            v => v == DeviceType.Phone ? "PHONE" : "WATCH",
            v => v == "PHONE" ? DeviceType.Phone : DeviceType.Watch);
        b.Property(x => x.Status).HasConversion(v => StatusToDb(v), v => StatusFromDb(v));
    }

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
