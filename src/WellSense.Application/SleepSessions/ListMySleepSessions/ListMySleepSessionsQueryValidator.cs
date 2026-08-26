using FluentValidation;

namespace WellSense.Application.SleepSessions.ListMySleepSessions;

public class ListMySleepSessionsQueryValidator : AbstractValidator<ListMySleepSessionsQuery>
{
    public ListMySleepSessionsQueryValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 365);
    }
}
