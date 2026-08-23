namespace WellSense.Domain.Measurements;

/// <summary>Vocabulario de la API y de la BD (CHECK de la migración 008) para SyncStatus.</summary>
public static class SyncStatusExtensions
{
    public static string ToWireString(this SyncStatus status) => status switch
    {
        SyncStatus.Processing => "PROCESSING",
        SyncStatus.Completed => "COMPLETED",
        SyncStatus.Failed => "FAILED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
