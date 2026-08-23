using FluentValidation;

namespace WellSense.Application.Profiles.UpsertMyProfile;

public class UpsertMyProfileCommandValidator : AbstractValidator<UpsertMyProfileCommand>
{
    public UpsertMyProfileCommandValidator()
    {
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.Occupation).MaximumLength(150);
        RuleFor(x => x.AvatarUrl).MaximumLength(2048);

        RuleFor(x => x.BirthDate)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow)).WithMessage("La fecha de nacimiento no puede ser futura.")
            .GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-120)).WithMessage("Fecha de nacimiento no válida.")
            .When(x => x.BirthDate is not null);

        RuleFor(x => x.WeightKg)
            .InclusiveBetween(1, 500).WithMessage("El peso debe estar entre 1 y 500 kg.")
            .When(x => x.WeightKg is not null);

        RuleFor(x => x.HeightCm)
            .InclusiveBetween(30, 272).WithMessage("La estatura debe estar entre 30 y 272 cm.")
            .When(x => x.HeightCm is not null);

        RuleFor(x => x.Timezone)
            .NotEmpty()
            .Must(BeAValidIanaTimezone)
            .WithMessage("La zona horaria debe ser un identificador IANA válido (ej. 'America/Mexico_City').");
    }

    // .NET en Linux resuelve identificadores IANA nativamente desde .NET 6 — no hace
    // falta ninguna librería adicional (NodaTime, TimeZoneConverter, etc.) para este
    // chequeo de validez.
    private static bool BeAValidIanaTimezone(string tz)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(tz);
            return true;
        }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}
