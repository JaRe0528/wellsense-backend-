namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Genera secretos aleatorios criptográficamente seguros para refresh tokens y
/// tokens de un solo uso (verify email / reset password). Devuelve el valor en claro
/// (para entregarlo al cliente) — el llamador es responsable de solo persistir el hash.
/// </summary>
public interface ITokenGenerator
{
    string GenerateUrlSafeToken(int byteLength = 32);

    /// <summary>Código numérico de 6 dígitos (000000-999999) para vinculación de dispositivo.</summary>
    string GenerateSixDigitCode();

    string Sha256Hex(string value);
}
