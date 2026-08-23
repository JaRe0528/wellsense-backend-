using FluentValidation;

namespace WellSense.Application.Notifications.RegisterToken;

public class RegisterNotificationTokenCommandValidator : AbstractValidator<RegisterNotificationTokenCommand>
{
    public RegisterNotificationTokenCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.FcmToken).NotEmpty().MaximumLength(4096); // los tokens de FCM son largos, sin un tamaño fijo garantizado
    }
}
