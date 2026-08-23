using FluentValidation;

namespace WellSense.Application.Memberships.SubscribeToPlan;

public class SubscribeToPlanCommandValidator : AbstractValidator<SubscribeToPlanCommand>
{
    public SubscribeToPlanCommandValidator()
    {
        RuleFor(x => x.PlanCode)
            .NotEmpty()
            .Must(c => c is "FREE" or "BASIC" or "PRO" or "PROFESSIONAL")
            .WithMessage("planCode debe ser uno de: FREE, BASIC, PRO, PROFESSIONAL.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
