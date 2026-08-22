using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WellSense.Domain.Identity;
using WellSense.Domain.Profiles;

namespace WellSense.Infrastructure.Persistence.Configurations;

public class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> b)
    {
        b.ToTable("profiles");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne<User>().WithOne().HasForeignKey<Profile>(x => x.UserId);
    }
}

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> b)
    {
        b.ToTable("goals");
        b.HasKey(x => x.Id);
        b.HasOne<Profile>().WithMany().HasForeignKey(x => x.ProfileId);
    }
}

public class OnboardingSurveyConfiguration : IEntityTypeConfiguration<OnboardingSurvey>
{
    public void Configure(EntityTypeBuilder<OnboardingSurvey> b)
    {
        b.ToTable("onboarding_surveys");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.ProfileId).IsUnique();
        b.HasOne<Profile>().WithOne().HasForeignKey<OnboardingSurvey>(x => x.ProfileId);
        b.Property(x => x.DeclaredStressLevel).HasConversion(v => StressLevelToDb(v), v => StressLevelFromDb(v));
    }

    // Ver el mismo comentario en IdentityConfigurations.UserConfiguration: extraer el
    // switch a un método estático con tipo de retorno explícito evita CS8514 cuando se
    // pasa directo como argumento de HasConversion.
    private static string StressLevelToDb(DeclaredStressLevel v) => v switch
    {
        DeclaredStressLevel.MuyBajo => "MUY_BAJO",
        DeclaredStressLevel.Bajo => "BAJO",
        DeclaredStressLevel.Alto => "ALTO",
        DeclaredStressLevel.MuyAlto => "MUY_ALTO",
        _ => "MODERADO"
    };

    private static DeclaredStressLevel StressLevelFromDb(string v) => v switch
    {
        "MUY_BAJO" => DeclaredStressLevel.MuyBajo,
        "BAJO" => DeclaredStressLevel.Bajo,
        "ALTO" => DeclaredStressLevel.Alto,
        "MUY_ALTO" => DeclaredStressLevel.MuyAlto,
        _ => DeclaredStressLevel.Moderado
    };
}
