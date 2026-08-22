using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WellSense.Application.Common.Interfaces;
using WellSense.Infrastructure.Persistence;

namespace WellSense.Tests.Integration;

/// <summary>
/// Host de pruebas end-to-end real (Kestrel de pruebas + pipeline HTTP completo:
/// autenticación JWT, rate limiting, middleware de excepciones) tal como pide el
/// riesgo abierto #2 del HANDOFF de Bloque 2.
///
/// Decisión: en vez de levantar un Postgres real vía Testcontainers (que requiere
/// Docker — no disponible de forma confiable en todos los entornos de CI/sandbox),
/// se reemplaza el DbContext por el proveedor InMemory de EF DESPUÉS de que el host
/// ya construyó la configuración real (Jwt/DeviceLink/RateLimiting siguen siendo la
/// configuración real de `appsettings.json`, no un stub). Esto es válido para lo que
/// estas pruebas verifican — el pipeline HTTP (JWT bearer, rate limiting, manejo de
/// errores) — porque ninguno de esos tres depende de Postgres específicamente. Lo que
/// NO cubren estas pruebas es el comportamiento de índices únicos/parciales reales
/// (eso ya está validado por separado en HANDOFF-DB y en el Bloque 1 de este repo).
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public readonly string DbName = $"wellsense-e2e-{Guid.NewGuid()}";
    public CapturingEmailSender CapturedEmails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Solo lo mínimo para que Program.cs no falle al arrancar (los
                // `?? throw` de Jwt:Secret/DeviceLink:Pepper/ConnectionStrings:Postgres) —
                // el resto de la configuración (IpRateLimiting, Jwt:Issuer/Audience/
                // AccessTokenMinutes) SÍ es la real de appsettings.json.
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Jwt:Secret"] = "test-secret-please-not-in-prod-32bytes-min!!",
                ["DeviceLink:Pepper"] = "test-pepper-please-not-in-prod"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Descarta el registro real de Npgsql y lo reemplaza por InMemory. No basta
            // con quitar DbContextOptions<WellSenseDbContext> — Npgsql ya había agregado
            // sus propios servicios internos (IDatabaseProvider, etc.) al contenedor de
            // la app vía TryAddEnumerable en Program.cs/AddInfrastructure, y esos NO se
            // remueven con RemoveAll<DbContextOptions<...>>(). Si InMemory usa el mismo
            // contenedor, EF ve dos proveedores registrados a la vez y lanza
            // InvalidOperationException ("Only a single database provider can be
            // registered"). La solución es darle a InMemory su PROPIO ServiceProvider
            // interno (UseInternalServiceProvider), aislado del de la app, para que no
            // se mezcle con lo que Npgsql ya registró.
            services.RemoveAll<DbContextOptions<WellSenseDbContext>>();
            services.AddDbContext<WellSenseDbContext>(options =>
            {
                options.UseInMemoryDatabase(DbName);
                options.UseInternalServiceProvider(
                    new ServiceCollection()
                        .AddEntityFrameworkInMemoryDatabase()
                        .BuildServiceProvider());
            });

            // Reemplaza el envío de email por un espía para poder leer el token de
            // verificación/reset en las pruebas (el flujo real solo lo loguea).
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(CapturedEmails);
        });
    }
}
