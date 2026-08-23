using FluentValidation;

namespace WellSense.Application.Sync.SyncMeasurements;

/// <summary>
/// Solo valida la FORMA del batch completo (todo-o-nada) — nunca el contenido de
/// mediciones individuales. Eso es a propósito: un batch de 500 mediciones con 1
/// dañada no debe rechazar las otras 499 con un 400 — ese es el trabajo del handler,
/// que clasifica cada medición en aceptada/duplicada/rechazada sin tumbar el batch
/// completo. Ver HANDOFF de este bloque.
/// </summary>
public class SyncMeasurementsCommandValidator : AbstractValidator<SyncMeasurementsCommand>
{
    public const int MaxBatchSize = 500;

    public SyncMeasurementsCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Measurements)
            .NotEmpty().WithMessage("El batch no puede venir vacío.")
            .Must(m => m.Count <= MaxBatchSize).WithMessage($"Un batch no puede tener más de {MaxBatchSize} mediciones.");
    }
}
