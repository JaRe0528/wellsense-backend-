using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// Confirma que el rate limiting por IP configurado en appsettings.json
/// (IpRateLimiting:GeneralRules) realmente bloquea en el pipeline HTTP real —
/// era el riesgo abierto #2 del HANDOFF de Bloque 2. Ambas rutas comparten el
/// mismo factory/host (buckets de conteo distintos por patrón de endpoint, así
/// que no se contaminan entre sí).
/// </summary>
public class RateLimitingEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Login_beyond_configured_limit_returns_429()
    {
        // appsettings.json: post:/api/v1/auth/login → 10 por minuto por IP.
        var client = factory.CreateClient();
        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i < 11; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
                new LoginRequest("no-existe@example.com", "cualquiera"));
        }

        lastResponse!.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task Device_link_redeem_beyond_configured_limit_returns_429()
    {
        // appsettings.json: post:/api/v1/auth/device-link/redeem → 5 por minuto por IP.
        // P0 no negociable — es la única defensa real contra fuerza bruta de códigos
        // de 6 dígitos (ver HANDOFF-DB §8 riesgo 7 y HANDOFF de Bloque 2).
        var client = factory.CreateClient();
        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i < 6; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/device-link/redeem",
                new RedeemDeviceLinkCodeRequest("000000", null, null, null));
        }

        lastResponse!.StatusCode.Should().Be((HttpStatusCode)429);
    }
}
