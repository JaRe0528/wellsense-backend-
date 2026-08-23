using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Billing;
using WellSense.Infrastructure.Persistence;
using WellSense.Tests.TestHelpers;

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
    public FakePaymentGateway PaymentGateway { get; } = new();

    /// <summary>
    /// Desactivado por defecto: la enorme mayoría de las clases de prueba HTTP (Auth,
    /// Profile, DeviceSync) no están probando límites de tasa, pero comparten la MISMA
    /// instancia de factory (vía IClassFixture) entre varias pruebas de la misma clase —
    /// cada llamada a /register desde una prueba distinta cuenta contra el mismo límite
    /// real (5/hora), así que la 6ta prueba de una clase con 6 registros recibía 429 en
    /// vez del 201 esperado, sin que hubiera ningún bug de negocio real detrás. Solo
    /// <see cref="RateLimitingEndpointTests"/> necesita el rate limiting real activo —
    /// usa <see cref="RateLimitedWebApplicationFactory"/> en su lugar.
    /// </summary>
    protected virtual bool EnableRateLimiting => false;

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
                ["DeviceLink:Pepper"] = "test-pepper-please-not-in-prod",
                ["RateLimiting:Enabled"] = EnableRateLimiting.ToString()
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
            //
            // IMPORTANTE: ese ServiceProvider interno se construye UNA SOLA VEZ aquí
            // afuera, no dentro del callback de `options =>`. `AddDbContext` registra
            // `DbContextOptions<TContext>` con lifetime Scoped por default, así que ese
            // callback se re-ejecuta en CADA scope, es decir, en CADA request HTTP. Si
            // `new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider()`
            // vivía adentro del callback, cada request armaba su propio ServiceProvider
            // interno nuevo y por lo tanto su propio store de InMemory vacío — mismo
            // `DbName`, pero un cache de store distinto que nunca lo había visto. Efecto
            // observado: los datos de un request (ej. `/register`) desaparecían en el
            // siguiente (`/verify-email`, `/login`), porque cada uno corría contra una
            // "base de datos" en memoria distinta y vacía. Capturando el ServiceProvider
            // en una variable ANTES de pasarlo a `AddDbContext`, el closure reutiliza la
            // misma instancia (y por lo tanto el mismo store) en cada scope/request.
            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.RemoveAll<DbContextOptions<WellSenseDbContext>>();
            services.AddDbContext<WellSenseDbContext>(options =>
            {
                options.UseInMemoryDatabase(DbName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });

            // Semilla del catálogo de planes (Bloque 6): la migración 012 real inserta
            // los 4 planes en Postgres vía el propio DDL, pero un InMemory store nuevo
            // arranca vacío — sin esto, cualquier prueba de integración que toque
            // /memberships/* fallaría por no encontrar ningún MembershipPlan. Se siembra
            // aquí, contra el mismo ServiceProvider/DbName que usará la app (mismo
            // mecanismo de "varios DbContext comparten un store InMemory por nombre" ya
            // usado en el Bloque 5 para simular la carrera de sync).
            using (var seedContext = new WellSenseDbContext(new DbContextOptionsBuilder<WellSenseDbContext>()
                .UseInMemoryDatabase(DbName)
                .UseInternalServiceProvider(inMemoryServiceProvider)
                .Options))
            {
                if (!seedContext.MembershipPlans.Any())
                {
                    seedContext.MembershipPlans.AddRange(
                        new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Free, Name = "Free", PriceCents = 0, Currency = "MXN" },
                        new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Basic, Name = "Basic", PriceCents = 9900, Currency = "MXN" },
                        new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Pro, Name = "Pro", PriceCents = 19900, Currency = "MXN" },
                        new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Professional, Name = "Professional", PriceCents = 39900, Currency = "MXN" });
                    seedContext.SaveChanges();
                }
            }

            // Reemplaza el envío de email por un espía para poder leer el token de
            // verificación/reset en las pruebas (el flujo real solo lo loguea).
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(CapturedEmails);

            // Reemplaza el gateway real de Stripe por uno falso (Bloque 6) — sin esto,
            // cualquier prueba de integración que intentara cobrar un plan pago recibiría
            // 503 PAYMENT_GATEWAY_NOT_CONFIGURED (no hay Stripe:SecretKey real en
            // pruebas, ni debería haberlo). El fake se expone como propiedad pública para
            // que cada prueba configure si el próximo cobro se aprueba o se rechaza.
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(PaymentGateway);
        });
    }
}

/// <summary>
/// Única factory con rate limiting real activo — exclusiva de
/// <see cref="RateLimitingEndpointTests"/>. El resto de las clases de integración
/// (Auth, Profile, DeviceSync) deben usar <see cref="CustomWebApplicationFactory"/> a
/// secas (rate limiting desactivado), porque no están probando límites de tasa y
/// comparten la misma instancia de factory entre varias pruebas de su clase — con el
/// límite real activo, esas pruebas se contaminan entre sí sin que haya ningún bug de
/// negocio detrás (ver el HANDOFF del bloque donde se corrigió esto).
/// </summary>
public class RateLimitedWebApplicationFactory : CustomWebApplicationFactory
{
    protected override bool EnableRateLimiting => true;
}
