using FluentValidation;

namespace WellSense.Application.Admin.BootstrapFirstAdmin;

public class BootstrapFirstAdminCommandValidator : AbstractValidator<BootstrapFirstAdminCommand>
{
    public BootstrapFirstAdminCommandValidator()
    {
        RuleFor(x => x.Secret).NotEmpty();
    }
}
