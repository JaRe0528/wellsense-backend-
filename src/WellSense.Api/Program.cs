using System.Text;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
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

// ---------- JWT Bearer ----------
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException(
        "Falta Jwt:Secret. En desarrollo: `dotnet user-secrets set \"Jwt:Secret\" \"...\"`. " +
        "En producción viene de Vault/Key Vault — nunca de appsettings.json versionado.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sin esto, el handler de JWT de ASP.NET Core remapea "sub"/"email" a URIs
        // largas de ClaimTypes por compatibilidad histórica con WS-Federation — con
        // MapInboundClaims=false, CurrentUserService puede leer los claims tal como
        // los emitió JwtTokenService (JwtRegisteredClaimNames.Sub, .Email).
        options.MapInboundClaims = false;
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

app.UseIpRateLimiting();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas de integración.
public partial class Program { }
