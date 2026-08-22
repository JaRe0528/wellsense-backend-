using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// Pruebas HTTP end-to-end reales: pasan por Kestrel de pruebas, el middleware de
/// excepciones, el pipeline de autenticación JWT y los controladores tal cual los
/// vería un cliente real — no invocan los handlers de MediatR directamente (eso ya
/// lo cubren las pruebas unitarias en tests/WellSense.Tests/Auth/).
/// </summary>
public class AuthFlowEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Password123";

    [Fact]
    public async Task Full_web_flow_register_verify_login_and_call_protected_endpoint()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, Password));
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // El token de verificación nunca viaja por HTTP (solo por "correo") — se
        // recupera del espía de email registrado en la factory.
        factory.CapturedEmails.VerificationTokens.Should().ContainKey(email);
        var verificationToken = factory.CapturedEmails.VerificationTokens[email];

        var verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/verify-email",
            new VerifyEmailRequest(verificationToken));
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(email, Password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        tokens.Should().NotBeNull();
        tokens!.AccessToken.Should().NotBeNullOrEmpty();

        // Endpoint protegido con el access token real emitido por /login — confirma
        // que el JWT bearer wireado en Program.cs valida issuer/audience/firma de
        // verdad, no solo que el handler de MediatR "hubiera" aceptado el request.
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var changePasswordResponse = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest(Password, "NewPassword456"));
        changePasswordResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Protected_endpoint_without_bearer_token_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest("whatever", "whatever2"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_with_tampered_jwt_returns_401()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        var verificationToken = factory.CapturedEmails.VerificationTokens[email];
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(verificationToken));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();

        // Se altera el último carácter del JWT — rompe la firma sin cambiar el formato.
        var tampered = tokens!.AccessToken[..^1] + (tokens.AccessToken[^1] == 'a' ? 'b' : 'a');

        client.DefaultRequestHeaders.Authorization = new("Bearer", tampered);
        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new ChangePasswordRequest(Password, "NewPassword456"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_unverified_email_returns_403_with_error_code()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        // No se verifica el email a propósito.

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("EMAIL_NOT_VERIFIED");
    }
}
