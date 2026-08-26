using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

public class MembershipsFlowEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Password123";

    private async Task<HttpClient> RegisterVerifyAndLoginAsync()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        var token = factory.CapturedEmails.VerificationTokens[email];
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);
        return client;
    }

    [Fact]
    public async Task List_plans_works_without_authentication()
    {
        var client = factory.CreateClient(); // sin Authorization header

        var response = await client.GetAsync("/api/v1/memberships/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var plans = await response.Content.ReadFromJsonAsync<List<PlanResponse>>();
        plans.Should().HaveCount(4);
        plans.Should().Contain(p => p.Code == "FREE" && p.PriceCents == 0);
        plans.Should().Contain(p => p.Code == "PROFESSIONAL" && p.PriceCents == 39900);
    }

    [Fact]
    public async Task Get_my_membership_before_ever_subscribing_lazily_returns_free()
    {
        var client = await RegisterVerifyAndLoginAsync();

        var response = await client.GetAsync("/api/v1/memberships/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var membership = await response.Content.ReadFromJsonAsync<MembershipResponse>();
        membership!.PlanCode.Should().Be("FREE");
        membership.Status.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task Subscribing_to_a_paid_plan_with_approved_payment_activates_it_and_records_the_payment()
    {
        var client = await RegisterVerifyAndLoginAsync();
        factory.PaymentGateway.NextApproved = true;

        var response = await client.PostAsJsonAsync("/api/v1/memberships/subscribe",
            new SubscribeRequest("PRO", "tok_visa", $"idem-{Guid.NewGuid()}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SubscribeResponse>();
        result!.PlanCode.Should().Be("PRO");
        result.Status.Should().Be("ACTIVE");
        result.PaymentId.Should().NotBeNull();

        var paymentsResponse = await client.GetAsync("/api/v1/payments/me");
        var payments = await paymentsResponse.Content.ReadFromJsonAsync<List<PaymentResponse>>();
        payments.Should().ContainSingle(p => p.Status == "APPROVED" && p.PlanCode == "PRO");
    }

    [Fact]
    public async Task Subscribing_with_a_declined_card_returns_402_and_does_not_change_the_active_membership()
    {
        var client = await RegisterVerifyAndLoginAsync();
        factory.PaymentGateway.NextApproved = false;
        factory.PaymentGateway.NextDeclineReason = "insufficient_funds";

        var response = await client.PostAsJsonAsync("/api/v1/memberships/subscribe",
            new SubscribeRequest("PRO", "tok_declined", $"idem-{Guid.NewGuid()}"));

        response.StatusCode.Should().Be((HttpStatusCode)402);

        var membershipResponse = await client.GetAsync("/api/v1/memberships/me");
        var membership = await membershipResponse.Content.ReadFromJsonAsync<MembershipResponse>();
        membership!.PlanCode.Should().Be("FREE"); // sigue en FREE, el intento rechazado no cambió nada
    }

    [Fact]
    public async Task Subscribing_to_a_paid_plan_without_a_payment_token_returns_400()
    {
        var client = await RegisterVerifyAndLoginAsync();

        var response = await client.PostAsJsonAsync("/api/v1/memberships/subscribe",
            new SubscribeRequest("BASIC", null, $"idem-{Guid.NewGuid()}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_downgrades_an_active_paid_subscription_back_to_free()
    {
        var client = await RegisterVerifyAndLoginAsync();
        factory.PaymentGateway.NextApproved = true;
        await client.PostAsJsonAsync("/api/v1/memberships/subscribe", new SubscribeRequest("PRO", "tok_visa", $"idem-{Guid.NewGuid()}"));

        var cancelResponse = await client.PostAsync("/api/v1/memberships/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var membershipResponse = await client.GetAsync("/api/v1/memberships/me");
        var membership = await membershipResponse.Content.ReadFromJsonAsync<MembershipResponse>();
        membership!.PlanCode.Should().Be("FREE");
    }

    [Fact]
    public async Task Requires_authentication_for_me_and_subscribe_but_not_for_the_plans_catalog()
    {
        var client = factory.CreateClient(); // sin Authorization header

        var meResponse = await client.GetAsync("/api/v1/memberships/me");
        var subscribeResponse = await client.PostAsJsonAsync("/api/v1/memberships/subscribe", new SubscribeRequest("FREE", null, "idem"));

        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        subscribeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upgrading_from_an_active_paid_plan_to_a_higher_one_replaces_it_cleanly_over_http()
    {
        // Parte 6 del encargo: el caso real que faltaba probar explícitamente —
        // BASIC activo → PRO con tarjeta aprobada. A diferencia de FREE→pagado o
        // pagado→cancelar (ya cubiertos), este cruza dos suscripciones PAGADAS reales.
        var client = await RegisterVerifyAndLoginAsync();
        factory.PaymentGateway.NextApproved = true;

        var basicResponse = await client.PostAsJsonAsync("/api/v1/memberships/subscribe",
            new SubscribeRequest("BASIC", "tok_visa", $"idem-{Guid.NewGuid()}"));
        basicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var basicResult = await basicResponse.Content.ReadFromJsonAsync<SubscribeResponse>();

        var proResponse = await client.PostAsJsonAsync("/api/v1/memberships/subscribe",
            new SubscribeRequest("PRO", "tok_visa", $"idem-{Guid.NewGuid()}"));
        proResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var proResult = await proResponse.Content.ReadFromJsonAsync<SubscribeResponse>();

        // No quedan 2 suscripciones activas a la vez — la de PRO es una fila nueva, no la misma que BASIC.
        proResult!.SubscriptionId.Should().NotBe(basicResult!.SubscriptionId);
        proResult.PlanCode.Should().Be("PRO");
        proResult.Status.Should().Be("ACTIVE");

        // GET /memberships/me refleja el plan nuevo inmediatamente, sin ningún paso intermedio.
        var meResponse = await client.GetAsync("/api/v1/memberships/me");
        var membership = await meResponse.Content.ReadFromJsonAsync<MembershipResponse>();
        membership!.PlanCode.Should().Be("PRO");
        membership.SubscriptionId.Should().Be(proResult.SubscriptionId);

        // El pago nuevo (de PRO) quedó registrado como aprobado.
        var paymentsResponse = await client.GetAsync("/api/v1/payments/me");
        var payments = await paymentsResponse.Content.ReadFromJsonAsync<List<PaymentResponse>>();
        payments.Should().HaveCount(2); // uno por cada suscripción pagada (BASIC, luego PRO)
        payments.Should().Contain(p => p.PlanCode == "PRO" && p.Status == "APPROVED");
        payments.Should().Contain(p => p.PlanCode == "BASIC" && p.Status == "APPROVED");
    }
}
