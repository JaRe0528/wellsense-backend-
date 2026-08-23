using FluentAssertions;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Memberships.SubscribeToPlan;
using WellSense.Domain.Billing;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Memberships;

public class SubscribeToPlanCommandHandlerTests
{
    private static async Task SeedPlansAsync(WellSense.Infrastructure.Persistence.WellSenseDbContext db)
    {
        db.MembershipPlans.AddRange(
            new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Free, Name = "Free", PriceCents = 0, Currency = "MXN" },
            new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Basic, Name = "Basic", PriceCents = 9900, Currency = "MXN" },
            new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Pro, Name = "Pro", PriceCents = 19900, Currency = "MXN" }
        );
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Subscribing_to_free_plan_never_calls_the_payment_gateway()
    {
        using var db = InMemoryDbContextFactory.Create();
        await SeedPlansAsync(db);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var gateway = new FakePaymentGateway();
        var handler = new SubscribeToPlanCommandHandler(db, gateway, clock);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new SubscribeToPlanCommand(userId, "FREE", null, "idem-1"), default);

        gateway.Charges.Should().BeEmpty();
        result.Status.Should().Be("ACTIVE");
        result.EndsAt.Should().BeNull(); // FREE no expira
        db.Payments.Should().BeEmpty(); // amount_cents > 0 en la BD — jamás se crea un Payment de $0
    }

    [Fact]
    public async Task Subscribing_to_paid_plan_without_token_throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        await SeedPlansAsync(db);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var gateway = new FakePaymentGateway();
        var handler = new SubscribeToPlanCommandHandler(db, gateway, clock);

        var act = () => handler.Handle(new SubscribeToPlanCommand(Guid.NewGuid(), "PRO", null, "idem-2"), default);

        await act.Should().ThrowAsync<PaymentDomainException>().Where(e => e.ErrorCode == "PAYMENT_METHOD_REQUIRED");
        gateway.Charges.Should().BeEmpty();
    }

    [Fact]
    public async Task Approved_payment_calls_the_gateway_exactly_once_and_creates_linked_payment_and_active_subscription()
    {
        using var db = InMemoryDbContextFactory.Create();
        await SeedPlansAsync(db);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var gateway = new FakePaymentGateway { NextApproved = true };
        var handler = new SubscribeToPlanCommandHandler(db, gateway, clock);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new SubscribeToPlanCommand(userId, "PRO", "tok_visa", "idem-3"), default);

        gateway.Charges.Should().ContainSingle(); // EXACTAMENTE una llamada — nunca cobra dos veces
        gateway.Charges.Single().AmountCents.Should().Be(19900); // el monto lo decide el servidor a partir del plan, no el cliente

        var subscription = db.Subscriptions.Single(s => s.Id == result.SubscriptionId);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.EndsAt.Should().Be(clock.UtcNow.AddMonths(1));

        var payment = db.Payments.Single();
        payment.Status.Should().Be(PaymentStatus.Approved);
        payment.SubscriptionId.Should().Be(subscription.Id);
        payment.CardLast4.Should().Be("4242");
    }

    [Fact]
    public async Task Declined_payment_creates_unlinked_payment_record_and_never_touches_subscriptions()
    {
        using var db = InMemoryDbContextFactory.Create();
        await SeedPlansAsync(db);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var gateway = new FakePaymentGateway { NextApproved = false, NextDeclineReason = "insufficient_funds" };
        var handler = new SubscribeToPlanCommandHandler(db, gateway, clock);
        var userId = Guid.NewGuid();

        var act = () => handler.Handle(new SubscribeToPlanCommand(userId, "PRO", "tok_declined", "idem-4"), default);

        await act.Should().ThrowAsync<PaymentDomainException>().Where(e => e.ErrorCode == "PAYMENT_DECLINED");

        var payment = db.Payments.Single();
        payment.Status.Should().Be(PaymentStatus.Declined);
        payment.SubscriptionId.Should().BeNull();
        db.Subscriptions.Should().BeEmpty(); // el intento fallido no crea ni toca ninguna suscripción
    }

    [Fact]
    public async Task Subscribing_to_a_new_plan_deactivates_the_previous_active_subscription_and_keeps_only_one_active()
    {
        using var db = InMemoryDbContextFactory.Create();
        await SeedPlansAsync(db);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var gateway = new FakePaymentGateway { NextApproved = true };
        var handler = new SubscribeToPlanCommandHandler(db, gateway, clock);
        var userId = Guid.NewGuid();

        await handler.Handle(new SubscribeToPlanCommand(userId, "BASIC", "tok_1", "idem-5"), default);
        var secondResult = await handler.Handle(new SubscribeToPlanCommand(userId, "PRO", "tok_2", "idem-6"), default);

        var allSubscriptions = db.Subscriptions.Where(s => s.UserId == userId).ToList();
        allSubscriptions.Should().HaveCount(2); // se conserva el historial, no se sobreescribe
        allSubscriptions.Count(s => s.Status == SubscriptionStatus.Active).Should().Be(1); // pero solo una activa a la vez
        allSubscriptions.Single(s => s.Status == SubscriptionStatus.Active).Id.Should().Be(secondResult.SubscriptionId);
        allSubscriptions.Single(s => s.Status == SubscriptionStatus.Canceled).EndsAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Unknown_plan_code_throws_plan_not_found()
    {
        using var db = InMemoryDbContextFactory.Create();
        await SeedPlansAsync(db);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var handler = new SubscribeToPlanCommandHandler(db, new FakePaymentGateway(), clock);

        var act = () => handler.Handle(new SubscribeToPlanCommand(Guid.NewGuid(), "NOT_A_PLAN", null, "idem-7"), default);

        await act.Should().ThrowAsync<PaymentDomainException>().Where(e => e.ErrorCode == "PLAN_NOT_FOUND");
    }
}
