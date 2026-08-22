using FluentValidation;

namespace WellSense.Application.Auth.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(320)
            .EmailAddress().WithMessage("El email no tiene un formato válido.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.")
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
    }
}
