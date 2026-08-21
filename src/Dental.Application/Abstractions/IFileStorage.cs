namespace Dental.Application.Abstractions;

/// <summary>Dosya deposu soyutlaması: dev'de lokal disk, üretimde S3-uyumlu depo. DB'ye blob yazılmaz.</summary>
public interface IFileStorage
{
    /// <returns>Depolama anahtarı (StorageKey).</returns>
    Task<string> SaveAsync(long tenantId, string category, string fileName, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);
}
