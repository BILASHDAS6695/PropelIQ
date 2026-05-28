namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Persists clinical document files to a configured storage backend.
/// All data is encrypted with AES-256-CBC before writing.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>
    /// Encrypts <paramref name="content"/> and writes it to the storage backend.
    /// </summary>
    /// <param name="originalFileName">
    ///   The original upload filename. Used to derive the stored filename.
    ///   A UUID suffix is appended to guarantee uniqueness.
    /// </param>
    /// <param name="content">Raw (unencrypted) file stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///   A tuple of the <c>storagePath</c> (relative to BasePath) and the
    ///   hex-encoded <c>encryptionIv</c> needed to decrypt the file.
    /// </returns>
    Task<(string StoragePath, string EncryptionIv)> SaveAsync(
        string originalFileName,
        Stream content,
        CancellationToken ct);

    /// <summary>
    /// Decrypts the stored file and returns a readable in-memory stream.
    /// Caller is responsible for disposing the returned stream.
    /// </summary>
    Task<Stream> ReadAsync(string storagePath, string encryptionIv, CancellationToken ct);

    /// <summary>
    /// Deletes the encrypted file at <paramref name="storagePath"/> (best-effort).
    /// Used for cleanup when DB persistence fails after a successful file write.
    /// </summary>
    void Delete(string storagePath);
}
