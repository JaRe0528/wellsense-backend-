using FluentValidation;

namespace WellSense.Application.Admin.UpdateUserStatus;

public class UpdateUserStatusCommandValidator : AbstractValidator<UpdateUserStatusCommand>
{
    public UpdateUserStatusCommandValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.Status)
            .NotEmpty()
            // Deliberadamente solo ACTIVE/SUSPENDED — PENDING existe en el CHECK de la
            // BD pero ningún flujo actual lo asigna (RegisterCommandHandler, Bloque 2,
            // deja el status en ACTIVE desde el registro) — no es una acción
            // administrativa real hoy, así que no se expone acá. Ver HANDOFF.
            .Must(s => s is "ACTIVE" or "SUSPENDED")
            .WithMessage("status debe ser ACTIVE o SUSPENDED.");
    }
}
