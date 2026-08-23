using FluentValidation;

namespace WellSense.Application.Profiles.Goals.AddGoal;

public class AddGoalCommandValidator : AbstractValidator<AddGoalCommand>
{
    public AddGoalCommandValidator()
    {
        RuleFor(x => x.Type).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TargetValue).GreaterThan(0);
    }
}
