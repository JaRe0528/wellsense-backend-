using FluentValidation;

namespace WellSense.Application.ActivitySessions.ListMyActivitySessions;

public class ListMyActivitySessionsQueryValidator : AbstractValidator<ListMyActivitySessionsQuery>
{
    public ListMyActivitySessionsQueryValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 365);
    }
}
