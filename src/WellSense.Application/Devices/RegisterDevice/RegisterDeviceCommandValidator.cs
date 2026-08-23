using FluentValidation;

namespace WellSense.Application.Devices.RegisterDevice;

public class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => t is "PHONE" or "WATCH")
            .WithMessage("type debe ser 'PHONE' o 'WATCH'.");
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.OsVersion).MaximumLength(50);
        RuleFor(x => x.AppVersion).MaximumLength(50);
    }
}
