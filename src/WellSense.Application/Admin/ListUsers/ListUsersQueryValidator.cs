using FluentValidation;

namespace WellSense.Application.Admin.ListUsers;

public class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.StatusFilter)
            .Must(s => s is "ACTIVE" or "SUSPENDED" or "PENDING")
            .WithMessage("statusFilter debe ser ACTIVE, SUSPENDED o PENDING.")
            .When(x => x.StatusFilter is not null);
    }
}
