using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WellSense.Infrastructure.Persistence;
using Xunit;

namespace WellSense.Tests;

/// <summary>
/// Bloque 1: solo humo de que el modelo de EF construye sin lanzar excepciones
/// (nombres de tabla snake_case, conversiones de enum, claves compuestas de
/// `measurements`, etc.). Las pruebas de integración reales contra Postgres
/// (Testcontainers) y las pruebas de endpoints de Auth se agregan en el Bloque 2,
/// cuando exista lógica de negocio que probar.
/// </summary>
public class InfrastructureSmokeTests
{
    [Fact]
    public void DbContext_model_builds_without_throwing()
    {
        var options = new DbContextOptionsBuilder<WellSenseDbContext>()
            .UseNpgsql("Host=localhost;Database=placeholder;Username=placeholder;Password=placeholder")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var context = new WellSenseDbContext(options);

        var act = () => context.Model.GetEntityTypes().ToList();

        act.Should().NotThrow();
        context.Model.GetEntityTypes().Should().HaveCount(26); // las 26 tablas mapeadas en Bloque 1
    }
}
