namespace WellSense.Domain.Profiles;

/// <summary>
/// Vocabulario de la API (y de la BD, ver migración 003 / HANDOFF-DB) para
/// DeclaredStressLevel — MUY_BAJO/BAJO/MODERADO/ALTO/MUY_ALTO, no los nombres de enum
/// de C# (MuyBajo, etc). Se usa tanto en el contrato de la API (Application/Api) como,
/// de forma independiente, en la conversión EF↔columna de texto (Infrastructure) — mismo
/// vocabulario, dos capas distintas, a propósito no comparten código entre sí (Domain no
/// depende de EF, y esto no depende de EF tampoco).
/// </summary>
public static class DeclaredStressLevelExtensions
{
    public static string ToWireString(this DeclaredStressLevel level) => level switch
    {
        DeclaredStressLevel.MuyBajo => "MUY_BAJO",
        DeclaredStressLevel.Bajo => "BAJO",
        DeclaredStressLevel.Alto => "ALTO",
        DeclaredStressLevel.MuyAlto => "MUY_ALTO",
        _ => "MODERADO"
    };

    public static bool TryParseWireString(string? value, out DeclaredStressLevel level)
    {
        switch (value)
        {
            case "MUY_BAJO": level = DeclaredStressLevel.MuyBajo; return true;
            case "BAJO": level = DeclaredStressLevel.Bajo; return true;
            case "MODERADO": level = DeclaredStressLevel.Moderado; return true;
            case "ALTO": level = DeclaredStressLevel.Alto; return true;
            case "MUY_ALTO": level = DeclaredStressLevel.MuyAlto; return true;
            default: level = default; return false;
        }
    }
}
