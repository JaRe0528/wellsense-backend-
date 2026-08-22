using FluentValidation;

namespace WellSense.Application.Auth.DeviceLink;

public class RedeemDeviceLinkCodeCommandValidator : AbstractValidator<RedeemDeviceLinkCodeCommand>
{
    public RedeemDeviceLinkCodeCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$").WithMessage("El código debe tener exactamente 6 dígitos.");
    }
}
