using FluentValidation;

namespace WellSense.Application.Devices.UpdateDeviceHeartbeat;

public class UpdateDeviceHeartbeatCommandValidator : AbstractValidator<UpdateDeviceHeartbeatCommand>
{
    public UpdateDeviceHeartbeatCommandValidator()
    {
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.OsVersion).MaximumLength(50);
        RuleFor(x => x.AppVersion).MaximumLength(50);
    }
}
