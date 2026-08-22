using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WellSense.Domain.Identity;

namespace WellSense.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Role).HasConversion(
            v => v == UserRole.Admin ? "admin" : "user",
            v => v == "admin" ? UserRole.Admin : UserRole.User);
        b.Property(x => x.Status).HasConversion(v => StatusToDb(v), v => StatusFromDb(v));
        // el índice único ux_users_email_lower (lower(email) WHERE is_deleted = false)
        // es a nivel de motor y se crea en la migración 001; EF no necesita modelarlo
        // para poder consultar, solo para no intentar recrearlo si algún día se usa
        // `dotnet ef migrations add` de verdad — se documenta como HasIndex sin
        // traducción exacta a filtered+expression index, ver HANDOFF.
    }

    // Extraído del lambda de HasConversion: el compilador no puede inferir el tipo de
    // retorno de un `switch` inline pasado directo como argumento de HasConversion
    // (CS8514) sin una anotación de tipo explícita en cada rama — un método estático
    // con tipo de retorno declarado evita el problema por completo.
    private static string StatusToDb(UserStatus v) => v switch
    {
        UserStatus.Suspended => "suspended",
        UserStatus.Pending => "pending",
        _ => "active"
    };

    private static UserStatus StatusFromDb(string v) => v switch
    {
        "suspended" => UserStatus.Suspended,
        "pending" => UserStatus.Pending,
        _ => UserStatus.Active
    };
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        b.Ignore(x => x.IsActive);
    }
}

public class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> b)
    {
        b.ToTable("email_verification_tokens");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
    }
}

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.ToTable("password_reset_tokens");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Metadata).HasColumnType("jsonb");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).IsRequired(false);
    }
}
