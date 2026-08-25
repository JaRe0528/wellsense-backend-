using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// La prueba más importante de Bloque 9, por instrucción explícita: confirmar que un
/// usuario normal NUNCA pueda acceder a la superficie administrativa, y que reciba 403
/// (no 404) — una superficie administrativa, a diferencia de un dato privado de otro
/// usuario, debe dejar evidente que existe pero está prohibida.
///
/// Corrección tras la primera ronda de `dotnet test` real: "no existe ningún admin
/// todavía" es, a propósito, un estado GLOBAL de todo el sistema (ver
/// BootstrapFirstAdminCommandHandler) — pero varias pruebas de este archivo asumían
/// cada una ser "la primera" contra la MISMA base de datos InMemory compartida por
/// IClassFixture&lt;CustomWebApplicationFactory&gt;. Solo la que corriera primero
/// conseguía de verdad 204; el resto, correctamente según el propio diseño, recibía 409
/// — exactamente lo que pasó (3 de las 4 fallas de la corrida real). No era un bug de la
/// app, era un bug de aislamiento de estas pruebas. Cada prueba que necesita
/// "todavía no existe ningún admin" ahora crea y descarta su PROPIA factory aislada, en
/// vez de compartir la inyectada por la clase — las pruebas que no bootstrapean (403/401
/// sin admin de por medio) sí siguen usando la compartida, más barata.
/// </summary>
public class AdminFlowEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Password123";
    private const string BootstrapSecret = "test-admin-bootstrap-secret";

    private static async Task<(HttpClient client, string refreshToken, string email)> RegisterVerifyAndLoginAsync(CustomWebApplicationFactory targetFactory)
    {
        var client = targetFactory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        var token = targetFactory.CapturedEmails.VerificationTokens[email];
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);
        return (client, tokens.RefreshToken, email);
    }

    [Fact]
    public async Task A_normal_authenticated_user_gets_403_not_404_on_every_admin_endpoint()
    {
        var (client, _, _) = await RegisterVerifyAndLoginAsync(factory);
        var randomId = Guid.NewGuid();

        var listUsers = await client.GetAsync("/api/v1/admin/users");
        var getUser = await client.GetAsync($"/api/v1/admin/users/{randomId}");
        var updateStatus = await client.PutAsJsonAsync($"/api/v1/admin/users/{randomId}/status", new UpdateUserStatusRequest("SUSPENDED"));
        var listSubs = await client.GetAsync("/api/v1/admin/subscriptions");
        var stats = await client.GetAsync("/api/v1/admin/stats");

        listUsers.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        getUser.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        updateStatus.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        listSubs.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        stats.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_requests_get_401_not_403()
    {
        var client = factory.CreateClient(); // sin Authorization header

        var response = await client.GetAsync("/api/v1/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Normal_user_gets_403_on_audit_logs_too()
    {
        var (client, _, _) = await RegisterVerifyAndLoginAsync(factory);

        var response = await client.GetAsync("/api/v1/admin/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Bootstrap_then_refresh_grants_real_admin_access_over_http()
    {
        using var isolatedFactory = new CustomWebApplicationFactory();
        var (client, refreshToken, _) = await RegisterVerifyAndLoginAsync(isolatedFactory);

        // Antes del bootstrap: 403, como cualquier usuario normal.
        var before = await client.GetAsync("/api/v1/admin/stats");
        before.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var bootstrapResponse = await client.PostAsJsonAsync("/api/v1/admin/bootstrap", new BootstrapAdminRequest(BootstrapSecret));
        bootstrapResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // El access token YA EMITIDO sigue con el rol viejo (el JWT es stateless) —
        // hace falta refrescar para obtener uno nuevo con el rol actualizado. Ver
        // HANDOFF: esto es intencional, no un bug, y hay que comunicárselo a
        // Web/Android.
        var stillOldToken = await client.GetAsync("/api/v1/admin/stats");
        stillOldToken.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(refreshToken));
        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<RefreshResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", newTokens!.AccessToken);

        var afterRefresh = await client.GetAsync("/api/v1/admin/stats");
        afterRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Bootstrap_with_wrong_secret_returns_403_and_a_second_bootstrap_attempt_returns_409()
    {
        using var isolatedFactory = new CustomWebApplicationFactory();
        var (firstClient, _, _) = await RegisterVerifyAndLoginAsync(isolatedFactory);

        var wrongSecretResponse = await firstClient.PostAsJsonAsync("/api/v1/admin/bootstrap", new BootstrapAdminRequest("wrong-secret"));
        wrongSecretResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var correctResponse = await firstClient.PostAsJsonAsync("/api/v1/admin/bootstrap", new BootstrapAdminRequest(BootstrapSecret));
        correctResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (secondClient, _, _) = await RegisterVerifyAndLoginAsync(isolatedFactory);
        var secondAttempt = await secondClient.PostAsJsonAsync("/api/v1/admin/bootstrap", new BootstrapAdminRequest(BootstrapSecret));
        secondAttempt.StatusCode.Should().Be((HttpStatusCode)409);
    }

    [Fact]
    public async Task Suspending_a_user_via_admin_then_that_user_cannot_refresh_their_session()
    {
        using var isolatedFactory = new CustomWebApplicationFactory();
        var (adminClient, adminRefreshToken, _) = await RegisterVerifyAndLoginAsync(isolatedFactory);
        var bootstrapResponse = await adminClient.PostAsJsonAsync("/api/v1/admin/bootstrap", new BootstrapAdminRequest(BootstrapSecret));
        bootstrapResponse.StatusCode.Should().Be(HttpStatusCode.NoContent); // precondición explícita — si esto falla, todo lo de abajo fallaría por una razón distinta y confusa

        var refreshResponse = await adminClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(adminRefreshToken));
        var adminTokens = await refreshResponse.Content.ReadFromJsonAsync<RefreshResponse>();
        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", adminTokens!.AccessToken);

        var (_, targetRefreshToken, targetEmail) = await RegisterVerifyAndLoginAsync(isolatedFactory);

        var listResponse = await adminClient.GetAsync($"/api/v1/admin/users?email={Uri.EscapeDataString(targetEmail)}");
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResponse<AdminUserSummaryResponse>>();
        var targetId = list!.Items.Single().Id;

        var suspendResponse = await adminClient.PutAsJsonAsync($"/api/v1/admin/users/{targetId}/status", new UpdateUserStatusRequest("SUSPENDED"));
        suspendResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshAttempt = await isolatedFactory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(targetRefreshToken));
        refreshAttempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized); // la sesión fue revocada al suspender
    }

    [Fact]
    public async Task Admin_can_see_login_events_in_the_audit_log_after_a_normal_user_logs_in()
    {
        using var isolatedFactory = new CustomWebApplicationFactory();
        var (adminClient, adminRefreshToken, _) = await RegisterVerifyAndLoginAsync(isolatedFactory);
        var bootstrapResponse = await adminClient.PostAsJsonAsync("/api/v1/admin/bootstrap", new BootstrapAdminRequest(BootstrapSecret));
        bootstrapResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshResponse = await adminClient.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(adminRefreshToken));
        var adminTokens = await refreshResponse.Content.ReadFromJsonAsync<RefreshResponse>();
        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", adminTokens!.AccessToken);

        var (_, _, targetEmail) = await RegisterVerifyAndLoginAsync(isolatedFactory); // este login real es lo que Bloque 10 ahora audita

        var response = await adminClient.GetAsync("/api/v1/admin/audit-logs?action=login_succeeded");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var logs = await response.Content.ReadFromJsonAsync<PagedResponse<AuditLogItemResponse>>();

        logs!.Items.Should().Contain(l => l.UserEmail == targetEmail && l.Action == "login_succeeded");
    }
}
