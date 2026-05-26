namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Abstracts password hashing so the Application layer never references
/// a specific hashing library.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password and returns the hash string.</summary>
    string Hash(string plaintext);

    /// <summary>
    /// Verifies a plaintext password against a previously produced hash.
    /// Returns <c>true</c> when the password matches.
    /// </summary>
    bool Verify(string plaintext, string hash);
}
