using FluentValidation;

namespace WellSense.Application.Wellness.ComputeDailyScores;

public class ComputeDailyScoresCommandValidator : AbstractValidator<ComputeDailyScoresCommand>
{
    public ComputeDailyScoresCommandValidator()
    {
        // No se permite calcular para el futuro (respecto al UTC "ahora" — un margen
        // amplio a propósito, ya que "hoy" varía según la zona horaria del usuario y no
        // queremos rechazar un cálculo legítimo de "hoy" para alguien varias zonas
        // horarias adelante de UTC).
        RuleFor(x => x.Date)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            .WithMessage("No se pueden calcular puntajes para una fecha futura.")
            .When(x => x.Date is not null);
    }
}
