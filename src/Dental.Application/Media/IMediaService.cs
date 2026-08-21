using Dental.Domain.Enums;

namespace Dental.Application.Media;

public interface IMediaService
{
    /// <summary>
    /// Multipart yükleme: boyut limiti (Storage:MaxUploadBytes), içerik türü doğrulama (jpg/png/webp/pdf),
    /// SHA-256 hesaplama ve görüntülerde 320px thumbnail üretimi.
    /// </summary>
    Task<MediaFileDto> UploadAsync(MediaUploadRequest request, CancellationToken ct = default);

    /// <summary>Sistem üretimi dosyayı depoya + MediaFile'a yazar (onam imzası/PDF'i gibi).</summary>
    Task<MediaFileDto> SaveGeneratedAsync(GeneratedFileRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<MediaFileDto>> ListForPatientAsync(long patientId, MediaCategory? category, CancellationToken ct = default);

    /// <summary>Kayıt global tenant filter ile bulunur (IDOR koruması) ve içerik stream olarak döner.</summary>
    Task<MediaDownload> OpenDownloadAsync(long id, CancellationToken ct = default);

    Task<MediaDownload> OpenThumbnailAsync(long id, CancellationToken ct = default);

    /// <summary>Soft delete; fiziksel dosya (yasal iz) silinmez — kalıcı temizlik ileriki retention job'ının işi.</summary>
    Task DeleteAsync(long id, CancellationToken ct = default);
}
