namespace WellSense.Domain.Devices;

/// <summary>
/// Vocabulario de la API y de la BD (CHECK de la migración 015) para DeviceCommandType —
/// START_MONITORING/STOP_MONITORING/CHANGE_INTERVAL/SYNC_NOW/REQUEST_STATUS, no los
/// nombres de enum de C#. Mismo patrón que MeasurementTypeExtensions (Bloque 4) —
/// necesario aquí (a diferencia de DeviceCommandStatus, que sí coincide con
/// ToUpperInvariant()) porque los nombres tienen guion bajo en la BD.
/// </summary>
public static class DeviceCommandTypeExtensions
{
    public static string ToWireString(this DeviceCommandType type) => type switch
    {
        DeviceCommandType.StartMonitoring => "START_MONITORING",
        DeviceCommandType.StopMonitoring => "STOP_MONITORING",
        DeviceCommandType.ChangeInterval => "CHANGE_INTERVAL",
        DeviceCommandType.SyncNow => "SYNC_NOW",
        DeviceCommandType.RequestStatus => "REQUEST_STATUS",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static bool TryParseWireString(string? value, out DeviceCommandType type)
    {
        switch (value)
        {
            case "START_MONITORING": type = DeviceCommandType.StartMonitoring; return true;
            case "STOP_MONITORING": type = DeviceCommandType.StopMonitoring; return true;
            case "CHANGE_INTERVAL": type = DeviceCommandType.ChangeInterval; return true;
            case "SYNC_NOW": type = DeviceCommandType.SyncNow; return true;
            case "REQUEST_STATUS": type = DeviceCommandType.RequestStatus; return true;
            default: type = default; return false;
        }
    }
}
