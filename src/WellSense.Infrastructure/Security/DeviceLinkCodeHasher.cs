using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256(code, pepper). El pepper (`DeviceLink:Pepper`) NUNCA debe versionarse en
/// `appsettings.json` — en desarrollo va en User Secrets, en producción en Vault/Key
/// Vault (ver 01-ARQUITECTURA-Y-STACK.md, gestión de secretos). Este bloque solo lee la
/// configuración; quién la puebla en cada ambiente es responsabilidad del Chat DevSecOps.
/// </summary>
public class DeviceLinkCodeHasher(IConfiguration configuration) : IDeviceLinkCodeHasher
{
    public string Hash(string sixDigitCode)
    {
        var pepper = configuration["DeviceLink:Pepper"]
            ?? throw new InvalidOperationException(
                "Falta DeviceLink:Pepper en la configuración. Nunca debe tener un valor por defecto " +
                "hardcodeado — ver HANDOFF-DB §7 sobre por qué un hash sin pepper es inseguro aquí.");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(sixDigitCode));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
