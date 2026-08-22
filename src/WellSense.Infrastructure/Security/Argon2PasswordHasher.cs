using Isopoh.Cryptography.Argon2;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Security;

/// <summary>
/// Argon2id, como exige la convención del proyecto. Isopoh.Cryptography.Argon2 codifica
/// el salt y los parámetros dentro del propio hash (formato PHC estándar), así que
/// `Verify` no necesita recibir el salt por separado. Nunca loguear `password` ni `hash`.
/// </summary>
public class Argon2PasswordHasher : IPasswordHasher
{
    public string Hash(string password) => Argon2.Hash(password);

    public bool Verify(string password, string hash) => Argon2.Verify(hash, password);
}
