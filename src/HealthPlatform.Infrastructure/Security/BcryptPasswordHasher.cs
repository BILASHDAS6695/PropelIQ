using HealthPlatform.Application.Interfaces;
using BC = BCrypt.Net.BCrypt;

namespace HealthPlatform.Infrastructure.Security;

/// <summary>
/// BCrypt-backed password hasher using a work-factor of 12.
/// Satisfies NFR-014 and OWASP password storage recommendations.
/// </summary>
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plaintext) =>
        BC.HashPassword(plaintext, WorkFactor);

    public bool Verify(string plaintext, string hash) =>
        BC.Verify(plaintext, hash);
}
