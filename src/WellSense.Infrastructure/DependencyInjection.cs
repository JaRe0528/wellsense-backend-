using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WellSense.Application.Common.Interfaces;
using WellSense.Infrastructure.Common;
using WellSense.Infrastructure.Notifications;
using WellSense.Infrastructure.Payments;
using WellSense.Infrastructure.Persistence;
using WellSense.Infrastructure.Security;

namespace WellSense.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Falta la connection string 'Postgres' en la configuración.");

        services.AddDbContext<WellSenseDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(WellSenseDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                })
                // Convención snake_case desde el primer commit — ver 02-BASE-DE-DATOS.md.
                // Combinada con los nombres de tabla explícitos (ToTable) en cada
                // configuración, garantiza que el modelo de EF hable el mismo
                // idioma que el DDL ya validado en HANDOFF-DB.
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IWellSenseDbContext>(sp => sp.GetRequiredService<WellSenseDbContext>());

        services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITokenGenerator, TokenGenerator>();
        services.AddScoped<IDeviceLinkCodeHasher, DeviceLinkCodeHasher>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IUniqueConstraintViolationDetector, UniqueConstraintViolationDetector>();
        // Correo real (post-Bloque-10): SmtpEmailSender resuelve como IEmailSender;
        // LoggingEmailSender queda registrado también como clase concreta (no como
        // IEmailSender) para que SmtpEmailSender la use como fallback cuando
        // Smtp:Host no está configurado.
        services.AddScoped<LoggingEmailSender>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IPushNotificationSender, FirebaseCloudMessagingSender>();
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();

        return services;
    }
}
