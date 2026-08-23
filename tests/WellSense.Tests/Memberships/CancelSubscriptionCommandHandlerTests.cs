using FluentAssertions;
using MediatR;
using NSubstitute;
using WellSense.Application.Memberships.CancelSubscription;
using WellSense.Application.Memberships.SubscribeToPlan;
using WellSense.Domain.Billing;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Memberships;

public class CancelSubscriptionCommandHandlerTests
{
    [Fact]
    public async Task Cancel_sends_a_SubscribeToPlan_command_targeting_FREE()
    {
        // Unitaria a propósito: solo verifica QUÉ comando manda CancelSubscriptionCommandHandler,
        // no el comportamiento completo de SubscribeToPlanCommandHandler (eso ya lo
        // cubre SubscribeToPlanCommandHandlerTests) — evita duplicar esas pruebas aquí.
        var mediator = Substitute.For<ISender>();
        var userId = Guid.NewGuid();
        var handler = new CancelSubscriptionCommandHandler(mediator);

        await handler.Handle(new CancelSubscriptionCommand(userId), default);

        await mediator.Received(1).Send(
            Arg.Is<SubscribeToPlanCommand>(c => c.CurrentUserId == userId && c.PlanCode == "FREE" && c.PaymentMethodToken == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_end_to_end_via_the_real_handler_downgrades_to_free_without_calling_the_gateway()
    {
        // Complementaria: esta sí ejercita el handler real de SubscribeToPlan (a través
        // de una implementación mínima de ISender que solo enruta este único comando),
        // para confirmar el resultado real en BD, no solo qué se llamó.
        using var db = InMemoryDbContextFactory.Create();
        db.MembershipPlans.AddRange(
            new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Free, Name = "Free", PriceCents = 0, Currency = "MXN" },
            new MembershipPlan { Id = Guid.NewGuid(), Code = PlanCode.Pro, Name = "Pro", PriceCents = 19900, Currency = "MXN" });
        await db.SaveChangesAsync();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var gateway = new FakePaymentGateway { NextApproved = true };
        var userId = Guid.NewGuid();
        var subscribeHandler = new SubscribeToPlanCommandHandler(db, gateway, clock);

        await subscribeHandler.Handle(new SubscribeToPlanCommand(userId, "PRO", "tok", "idem-1"), default);
        gateway.Charges.Clear();

        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<SubscribeToPlanCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => subscribeHandler.Handle(ci.Arg<SubscribeToPlanCommand>(), ci.Arg<CancellationToken>()));

        var cancelHandler = new CancelSubscriptionCommandHandler(mediator);
        await cancelHandler.Handle(new CancelSubscriptionCommand(userId), default);

        gateway.Charges.Should().BeEmpty(); // bajar a FREE nunca cobra
        var active = db.Subscriptions.Single(s => s.UserId == userId && s.Status == SubscriptionStatus.Active);
        active.PlanId.Should().Be(db.MembershipPlans.Single(p => p.Code == PlanCode.Free).Id);
    }
}
