using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Medya arşivi kaydı. Blob DB'ye yazılmaz; StorageKey IFileStorage'ın (dev: lokal disk,
/// üretim: S3-uyumlu) göreli anahtarıdır. Sha256 yasal iz/bütünlük özeti.
/// </summary>
public class MediaFile : TenantEntity
{
    public long ClinicId { get; set; }
    /// <summary>Hastaya bağlı olmayan dosyalarda (logo, fatura UBL) NULL.</summary>
    public long? PatientId { get; set; }
    public MediaCategory Category { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    /// <summary>Depo anahtarı: {tenantId}/{category}/{yyyyMM}/{guid}{ext}.</summary>
    public required string StorageKey { get; set; }
    /// <summary>İçeriğin SHA-256 özeti (hex, 64 karakter).</summary>
    public required string Sha256 { get; set; }
    /// <summary>Görüntülerde 320px küçük resim anahtarı.</summary>
    public string? ThumbnailKey { get; set; }
    /// <summary>Çekim tarihi (röntgen/fotoğraf).</summary>
    public DateOnly? TakenAt { get; set; }
    public string? Description { get; set; }
    /// <summary>İlgili FDI diş numarası (periapikal röntgen vb.).</summary>
    public string? ToothNumber { get; set; }
    /// <summary>Sistem üretimi dosyalarda (public imza, job çıktısı) NULL.</summary>
    public long? UploadedByUserId { get; set; }
}
