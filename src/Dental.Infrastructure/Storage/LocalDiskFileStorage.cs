using Dental.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Dental.Infrastructure.Storage;

/// <summary>
/// IFileStorage'ın lokal disk sürücüsü (dev varsayılanı; üretimde S3-uyumlu sürücüyle değiştirilecek).
/// Yol düzeni: {root}/{tenantId}/{category}/{yyyyMM}/{guid}{ext}; StorageKey köke göre göreli tutulur.
/// Tüm anahtar çözümlemeleri path traversal'a karşı kök altında kalmaya zorlanır.
/// </summary>
public sealed class LocalDiskFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalRoot);

    public async Task<string> SaveAsync(long tenantId, string category, string fileName, Stream content, CancellationToken ct = default)
    {
        var safeCategory = Sanitize(category, fallback: "misc");
        var extension = SanitizeExtension(Path.GetExtension(fileName));
        var storageKey = $"{tenantId}/{safeCategory}/{DateTime.UtcNow:yyyyMM}/{Guid.NewGuid():N}{extension}";

        var fullPath = ResolveSafePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var file = File.Create(fullPath);
        await content.CopyToAsync(file, ct);
        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var fullPath = ResolveSafePath(storageKey);
        if (!File.Exists(fullPath))
            throw new KeyNotFoundException("Dosya depoda bulunamadı.");
        return Task.FromResult<Stream>(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var fullPath = ResolveSafePath(storageKey);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolveSafePath(storageKey)));
    }

    /// <summary>Path traversal koruması: çözülen tam yol her zaman kökün altında kalmalıdır.</summary>
    private string ResolveSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new InvalidOperationException("Depolama anahtarı boş olamaz.");
        var fullPath = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Geçersiz depolama anahtarı.");
        return fullPath;
    }

    private static string Sanitize(string value, string fallback)
    {
        var safe = new string([.. value.Where(char.IsLetterOrDigit)]).ToLowerInvariant();
        return safe.Length == 0 ? fallback : safe;
    }

    private static string SanitizeExtension(string extension)
    {
        var safe = new string([.. extension.TrimStart('.').Where(char.IsLetterOrDigit)]).ToLowerInvariant();
        return safe.Length is 0 or > 10 ? "" : "." + safe;
    }
}
