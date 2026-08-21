using Dental.Domain.Enums;

namespace Dental.Application.Media;

/// <summary>Kullanıcı yüklemesi (multipart). Content akışı controller'dan gelir; boyut/tür doğrulaması serviste.</summary>
public sealed record MediaUploadRequest(
    long PatientId,
    MediaCategory Category,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    string? Description = null,
    string? ToothNumber = null,
    DateOnly? TakenAt = null);

/// <summary>Sistem üretimi dosya kaydı (imza PNG, onam PDF, fatura çıktısı...). Tür/boyut doğrulaması uygulanmaz.</summary>
public sealed record GeneratedFileRequest(
    long ClinicId,
    long? PatientId,
    MediaCategory Category,
    string FileName,
    string ContentType,
    byte[] Content,
    string? Description = null);

public sealed record MediaFileDto(
    long Id,
    long? PatientId,
    MediaCategory Category,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    bool HasThumbnail,
    DateOnly? TakenAt,
    string? Description,
    string? ToothNumber,
    long? UploadedByUserId,
    string? UploadedByName,
    DateTime CreatedAtUtc);

/// <summary>İndirme akışı; controller stream'i FileStreamResult ile döner (dispose sorumluluğu framework'te).</summary>
public sealed record MediaDownload(Stream Content, string FileName, string ContentType);
