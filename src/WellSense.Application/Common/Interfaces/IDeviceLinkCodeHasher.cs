namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// HMAC-SHA256(code, pepper). El pepper vive fuera de la base de datos (Vault/Key Vault
/// en producción, User Secrets en desarrollo) — nunca en la BD ni en el código fuente.
/// Ver HANDOFF-DB §7: un hash simple sin pepper sobre solo 1,000,000 combinaciones es
/// reversible por fuerza bruta offline si se filtra la tabla.
/// </summary>
public interface IDeviceLinkCodeHasher
{
    string Hash(string sixDigitCode);
}
