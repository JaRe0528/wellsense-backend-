using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// Bloque 10 (hardening de código). CORS y los headers de seguridad son plomería que
/// solo se puede probar de verdad contra el pipeline HTTP real — no tiene sentido
/// probarlos a nivel de handler, no hay ningún handler involucrado.
/// </summary>
public class SecurityHardeningEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Every_response_carries_the_three_security_headers()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/memberships/plans"); // público, no requiere auth

        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.Should().ContainKey("Referrer-Policy");
    }

    [Fact]
    public async Task Security_headers_are_present_even_on_error_responses()
    {
        var client = factory.CreateClient();

        // Sin Authorization header -> 401, generado por el middleware de autenticación,
        // no por un controller — confirma que los headers de seguridad se aplican
        // incluso cuando el pipeline nunca llega a MapControllers.
        var response = await client.GetAsync("/api/v1/users/me");

        response.Headers.Should().ContainKey("X-Content-Type-Options");
    }

    [Fact]
    public async Task Allowed_origin_gets_the_cors_header_back()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://allowed.example.com");

        var response = await client.GetAsync("/api/v1/memberships/plans");

        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("https://allowed.example.com");
    }

    [Fact]
    public async Task Disallowed_origin_does_not_get_the_cors_header()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://evil.example.com");

        var response = await client.GetAsync("/api/v1/memberships/plans");

        // La request en sí no falla (CORS no bloquea del lado servidor una request
        // simple ya hecha — es el NAVEGADOR quien la bloquearía al no ver el header de
        // vuelta) — lo que se prueba es que el servidor nunca le dice al navegador que
        // ese origen está permitido.
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
