namespace HealthPlatform.Application.Settings;

public sealed class DocumentStorageSettings
{
    public const string SectionName = "DocumentStorage";

    /// <summary>Root directory for encrypted document files.</summary>
    public string BasePath { get; init; } = "documents";

    /// <summary>
    /// Base64-encoded 32-byte master AES-256 encryption key.
    /// Rotate via environment variable: DocumentStorage__EncryptionKey
    /// </summary>
    public string EncryptionKey { get; init; } = string.Empty;
}
