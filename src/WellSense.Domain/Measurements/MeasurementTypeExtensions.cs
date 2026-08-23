namespace WellSense.Domain.Measurements;

/// <summary>
/// Vocabulario de la API y de la BD (CHECK de la migración 006 / HANDOFF-DB) para
/// MeasurementType — HEART_RATE/STEPS/SPO2/CALORIES/SKIN_TEMP, no los nombres de enum
/// de C#. Mismo patrón que DeclaredStressLevelExtensions (Bloque 3): esto es
/// independiente de la conversión EF↔columna que vive en Infrastructure — no comparten
/// código a propósito, para que Domain siga sin depender de EF.
/// </summary>
public static class MeasurementTypeExtensions
{
    public static string ToWireString(this MeasurementType type) => type switch
    {
        MeasurementType.HeartRate => "HEART_RATE",
        MeasurementType.Steps => "STEPS",
        MeasurementType.Spo2 => "SPO2",
        MeasurementType.Calories => "CALORIES",
        MeasurementType.SkinTemp => "SKIN_TEMP",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static bool TryParseWireString(string? value, out MeasurementType type)
    {
        switch (value)
        {
            case "HEART_RATE": type = MeasurementType.HeartRate; return true;
            case "STEPS": type = MeasurementType.Steps; return true;
            case "SPO2": type = MeasurementType.Spo2; return true;
            case "CALORIES": type = MeasurementType.Calories; return true;
            case "SKIN_TEMP": type = MeasurementType.SkinTemp; return true;
            default: type = default; return false;
        }
    }
}
