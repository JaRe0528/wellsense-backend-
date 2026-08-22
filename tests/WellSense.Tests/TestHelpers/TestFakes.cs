using WellSense.Application.Common.Interfaces;

namespace WellSense.Tests.TestHelpers;

public class FixedClock(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

/// <summary>Hash reversible y rápido — solo para pruebas, nunca usar en producción.</summary>
public class PlainTextPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"HASH::{password}";
    public bool Verify(string password, string hash) => hash == $"HASH::{password}";
}

/// <summary>Genera tokens/códigos deterministas y crecientes para que las pruebas sean reproducibles.</summary>
public class SequentialTokenGenerator : ITokenGenerator
{
    private int _tokenCounter;
    private readonly Queue<string> _codes = new();

    public void EnqueueCode(string code) => _codes.Enqueue(code);

    public string GenerateUrlSafeToken(int byteLength = 32) => $"token-{++_tokenCounter}";

    public string GenerateSixDigitCode() => _codes.Count > 0 ? _codes.Dequeue() : (++_tokenCounter).ToString("D6");

    public string Sha256Hex(string value) => $"sha256::{value}";
}

public class FakeDeviceLinkCodeHasher : IDeviceLinkCodeHasher
{
    public string Hash(string sixDigitCode) => $"hmac::{sixDigitCode}";
}

public class FakeJwtTokenService : IJwtTokenService
{
    public string GenerateAccessToken(Guid userId, string email, string role) => $"jwt-for-{userId}";
}

public class ControllableViolationDetector : IUniqueConstraintViolationDetector
{
    private readonly bool _alwaysReturn;
    public ControllableViolationDetector(bool alwaysReturn = true) => _alwaysReturn = alwaysReturn;
    public bool IsUniqueViolation(Exception ex, string constraintName) => _alwaysReturn;
}
