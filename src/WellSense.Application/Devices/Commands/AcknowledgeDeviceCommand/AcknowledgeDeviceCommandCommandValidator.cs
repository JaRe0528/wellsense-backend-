using FluentValidation;

namespace WellSense.Application.Devices.Commands.AcknowledgeDeviceCommand;

public class AcknowledgeDeviceCommandCommandValidator : AbstractValidator<AcknowledgeDeviceCommandCommand>
{
    public AcknowledgeDeviceCommandCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.CommandId).NotEmpty();
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => s is "ACKNOWLEDGED" or "FAILED")
            .WithMessage("status debe ser ACKNOWLEDGED o FAILED — el resto de los estados los controla el backend.");
    }
}
