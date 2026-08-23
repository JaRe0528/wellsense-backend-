using FluentValidation;

namespace WellSense.Application.Users.DeleteMe;

public class DeleteMeCommandValidator : AbstractValidator<DeleteMeCommand>
{
    public DeleteMeCommandValidator()
    {
        // Exigir la contraseña actual para borrar la cuenta — igual de sensible (o más)
        // que cambiarla. Evita que un token robado de corta vida (15 min) baste para
        // destruir la cuenta sin que el atacante conozca la contraseña.
        RuleFor(x => x.CurrentPassword).NotEmpty();
    }
}
