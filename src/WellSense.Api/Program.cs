using System.Text;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using WellSense.Api.Hubs;
using WellSense.Api.Middleware;
using WellSense.Api.Services;
using WellSense.Application;
using WellSense.Application.Common.Interfaces;
using WellSense.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------- Serilog ----------
// Consola siempre; Seq opcional vía configuración (Serilog:WriteTo Seq en appsettings).
// Regla dura: nunca loguear password/CVV/AccessToken/RefreshToken en ningún sink — los
// handlers de Auth y este Program.cs fueron escritos para nunca pasar esos valores a
// ILogger, ni siquiera en los logs de "reuse detectado" o del stub de email.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName());

// ---------- Servicios base ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "WellSense API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Ingresa el JWT de acceso: Bearer {token}"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---------- SignalR (Bloque 5: dashboard en vivo; Bloque 8: comandos a dispositivos) ----------
builder.Services.AddSignalR();
builder.Services.AddScoped<IDashboardNotifier, SignalRDashboardNotifier>();
builder.Services.AddScoped<IDeviceCommandNotifier, SignalRDeviceCommandNotifier>();

// ---------- CORS (Bloque 10: hardening) ----------
// Whitelist explícita, nunca wildcard — `Cors:AllowedOrigins` es un array en
// appsettings.json (o User Secrets/variables de entorno en cada ambiente), nunca
// `AllowAnyOrigin()`. Un array vacío/ausente significa "ningún origen permitido" (falla
// cerrado, no abierto) — no un fallback silencioso a "todo permitido".
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()); // necesario para que el navegador mande el header Authorization en llamadas cross-origin
});

// ---------- JWT Bearer ----------
// IMPORTANTE: `Jwt:Secret` (y el resto de los valores de este bloque) se leen desde
// `builder.Configuration` DENTRO del callback de `AddJwtBearer`, no en una variable
// local capturada aquí arriba antes de `builder.Build()`. Motivo: `WebApplicationFactory`
// (usado en las pruebas de integración HTTP) aplica sus overrides de configuración
// (`ConfigureAppConfiguration`, ej. un `Jwt:Secret` de prueba) recién al reconstruir el
// host, es decir, DESPUÉS de que el código de nivel superior de este archivo ya corrió.
// Si se lee `Jwt:Secret` en una variable local aquí, esa lectura captura el valor real
// de `appsettings.json` (el placeholder `__SET_VIA_USER_SECRETS_OR_VAULT__...`) en vez
// del override de prueba, mientras que `JwtTokenService` (que FIRMA los tokens) lee
// `Jwt:Secret` vía `IConfiguration` inyectado en tiempo de request, viendo sí la
// configuración final ya fusionada con el override. Resultado observado: el token se
// firma con la clave de prueba pero se valida contra la clave del placeholder → firma
// "inválida" → 401 en cualquier endpoint protegido, incluso con un token legítimo.
// Leyendo todo esto dentro del callback (que ASP.NET Core invoca de forma perezosa,
// bien después de `Build()`) se evita el problema — mismo patrón que ya usa
// `DeviceLinkCodeHasher` con `DeviceLink:Pepper` vía `IConfiguration` inyectado.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sin esto, el handler de JWT de ASP.NET Core remapea "sub"/"email" a URIs
        // largas de ClaimTypes por compatibilidad histórica con WS-Federation — con
        // MapInboundClaims=false, CurrentUserService puede leer los claims tal como
        // los emitió JwtTokenService (JwtRegisteredClaimNames.Sub, .Email).
        options.MapInboundClaims = false;

        var jwtSecret = builder.Configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException(
                "Falta Jwt:Secret. En desarrollo: `dotnet user-secrets set \"Jwt:Secret\" \"...\"`. " +
                "En producción viene de Vault/Key Vault — nunca de appsettings.json versionado.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // SignalR (Bloque 5): un WebSocket no puede mandar un header Authorization normal
        // en el handshake del navegador — el patrón oficial de ASP.NET Core es leer el
        // JWT de un query string `access_token` en vez del header, PERO solo para
        // requests a los propios hubs (nunca para el resto de la Api, donde el header
        // sigue siendo obligatorio). El chequeo de path acota esto exactamente a esas
        // rutas — Bloque 8 agrega /hubs/device-commands al mismo chequeo.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                var isHubRequest = path.StartsWithSegments("/hubs/dashboard") || path.StartsWithSegments("/hubs/device-commands");
                if (!string.IsNullOrEmpty(accessToken) && isHubRequest)
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ---------- Rate limiting por IP (AspNetCoreRateLimit) ----------
// P0 no negociable en /login, /register, /forgot-password, /reset-password (web) y
// device-link/redeem (móvil) — ver appsettings.json:IpRateLimiting y el HANDOFF de
// este bloque para la justificación de cada regla.
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Flag leído de configuración (default true — nunca deshabilitado en producción por
// accidente). Existe específicamente para que las pruebas de integración puedan
// desactivar el rate limiting real sin tener que levantar servidores/relojes
// artificiales: la mayoría de las clases de prueba HTTP (Auth, Profile, DeviceSync)
// no están probando límites de tasa, y compartir la misma IpRateLimitOptions:GeneralRules
// real (ej. 5 registros/hora) entre varias pruebas de la misma clase — todas contra el
// mismo IWebHostBuilder/factory — las hacía fallar entre sí sin que hubiera ningún bug
// de negocio real. Solo RateLimitingEndpointTests necesita esto activo; ver
// CustomWebApplicationFactory/RateLimitedWebApplicationFactory en Tests.
if (app.Configuration.GetValue("RateLimiting:Enabled", true))
{
    app.UseIpRateLimiting();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapHub<DeviceCommandHub>("/hubs/device-commands");

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas de integración.
public partial class Program { }
