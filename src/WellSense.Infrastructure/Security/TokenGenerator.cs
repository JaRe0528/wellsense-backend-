using System.Security.Cryptography;
using System.Text;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Security;

public class TokenGenerator : ITokenGenerator
{
    public string GenerateUrlSafeToken(int byteLength = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string GenerateSixDigitCode()
    {
        // RandomNumberGenerator.GetInt32 es criptográficamente seguro y sin sesgo de
        // módulo (a diferencia de Random.Next % 1000000). Rango 0-999999 inclusive →
        // exactamente las 1,000,000 combinaciones que asume HANDOFF-DB.
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    public string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
