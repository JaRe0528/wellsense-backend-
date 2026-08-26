using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using WellSense.Domain.Identity;
using WellSense.Infrastructure.Persistence;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// Regresión del bug urgente de producción post-Bloque-10: `audit_logs.ip_address` era
/// `inet` nativo (migración 002), pero `AuditLog.IpAddress` (C#) nunca tuvo conversión —
/// EF/Npgsql mandaba un parámetro `text` contra una columna `inet` y Postgres lo
/// rechazaba (`42804`). NINGUNA prueba de este proyecto lo detectó porque el resto de
/// las ~200 pruebas de integración usan el proveedor InMemory de EF Core
/// (`CustomWebApplicationFactory`), que no aplica los tipos nativos de Postgres en
/// absoluto — un `string` se guarda como `string`, sin importar qué tipo real tendría la
/// columna. Esta es la PRIMERA prueba del proyecto que corre contra un Postgres real
/// (`Testcontainers.PostgreSql`, referenciado desde el Bloque 1 pero nunca usado hasta
/// ahora) — exactamente el hueco que permitió que este bug llegara a producción con
/// 196/196 pruebas en verde.
///
/// Aviso de honestidad: mi entorno de trabajo no tiene Docker disponible (`docker: not
/// found`), así que escribí y razoné esta prueba con el mismo cuidado que las demás, pero
/// NO pude correrla yo mismo para confirmar que pasa — a diferencia de todas las
/// validaciones anteriores contra Postgres real, que sí corrí directamente con psql. Esta
/// SÍ necesita que ustedes la corran (con Docker disponible, vía `dotnet test`) para
/// confirmarla. El fix en sí (migración 016) SÍ está validado por mi cuenta, con psql,
/// reproduciendo el error exacto de producción y confirmando que desaparece — ver
/// HANDOFF.
/// </summary>
public class AuditLogRealPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task Writing_an_audit_log_with_a_real_ip_address_does_not_throw()
    {
        var options = new DbContextOptionsBuilder<WellSenseDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new WellSenseDbContext(options);

        var user = new User
        {
            Id = Guid.NewGuid(), Email = "audit-regression@example.com", PasswordHash = "h",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "login_succeeded",
            Metadata = "{}",
            IpAddress = "203.0.113.42", // exactamente el tipo de valor que rompía antes de la migración 016
            CreatedAt = DateTimeOffset.UtcNow
        });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Writing_an_audit_log_with_a_null_ip_address_also_does_not_throw()
    {
        // El caso de login fallido con email inexistente (Bloque 10): UserId null,
        // IpAddress puede venir null también — confirma que la columna nullable sigue
        // aceptando NULL después del cambio de tipo.
        var options = new DbContextOptionsBuilder<WellSenseDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new WellSenseDbContext(options);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = null,
            Action = "login_failed",
            Metadata = "{\"reason\":\"invalid_credentials\"}",
            IpAddress = null,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        var migrationsDir = FindMigrationsDirectory();
        var upFiles = Directory.GetFiles(migrationsDir, "*_up.sql")
            .OrderBy(f => int.Parse(Path.GetFileName(f).Split('_')[0]))
            .ToList();

        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        foreach (var file in upFiles)
        {
            var sql = await File.ReadAllTextAsync(file);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string FindMigrationsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "sql-migrations")))
            dir = dir.Parent;
        if (dir is null)
            throw new DirectoryNotFoundException("No se encontró sql-migrations/ subiendo desde AppContext.BaseDirectory.");
        return Path.Combine(dir.FullName, "sql-migrations");
    }
}
