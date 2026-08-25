using FluentValidation;

namespace WellSense.Application.Admin.ListActiveSubscriptions;

public class ListActiveSubscriptionsQueryValidator : AbstractValidator<ListActiveSubscriptionsQuery>
{
    public ListActiveSubscriptionsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
