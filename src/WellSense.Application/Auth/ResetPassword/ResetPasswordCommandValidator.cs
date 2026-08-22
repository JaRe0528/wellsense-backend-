using FluentValidation;

namespace WellSense.Application.Auth.ResetPassword;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(10)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
    }
}
