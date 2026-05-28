using System.Security.Cryptography;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Infrastructure.Documents;

internal sealed class LocalDocumentStorageService : IDocumentStorageService
{
    private readonly DocumentStorageSettings _settings;
    private readonly ILogger<LocalDocumentStorageService> _logger;

    public LocalDocumentStorageService(
        IOptions<DocumentStorageSettings> options,
        ILogger<LocalDocumentStorageService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<(string StoragePath, string EncryptionIv)> SaveAsync(
        string originalFileName,
        Stream content,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_settings.BasePath);

        // Derive a unique filename to avoid collisions
        var ext = Path.GetExtension(originalFileName);
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        var stored = $"{baseName}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_settings.BasePath, stored);

        // Generate random 16-byte IV; master key comes from config
        var masterKey = Convert.FromBase64String(_settings.EncryptionKey);
        var iv = RandomNumberGenerator.GetBytes(16);
        var ivHex = Convert.ToHexString(iv);

        using var aes = Aes.Create();
        aes.Key = masterKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();

        await using var fileStream = new FileStream(
            fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await using var cryptoStream = new CryptoStream(fileStream, encryptor, CryptoStreamMode.Write);
        await content.CopyToAsync(cryptoStream, ct);
        await cryptoStream.FlushFinalBlockAsync(ct);

        _logger.LogInformation("Document encrypted and saved to {StoredPath}", stored);
        return (stored, ivHex);
    }

    public void Delete(string storagePath)
    {
        var fullPath = Path.Combine(_settings.BasePath, storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted orphaned document at {Path}", storagePath);
        }
    }
}
