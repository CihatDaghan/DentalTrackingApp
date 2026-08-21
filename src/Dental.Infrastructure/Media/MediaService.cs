using System.Security.Cryptography;
using Dental.Application.Abstractions;
using Dental.Application.Media;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Dental.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Dental.Infrastructure.Media;

/// <summary>
/// Medya arşivi: yükleme (boyut/tür doğrulama + SHA-256 + 320px thumbnail), listeleme,
/// indirme ve soft delete. İndirme/silme kayıtları global tenant filter'dan geçer —
/// başka kiracının medyası id tahminiyle erişilemez (IDOR koruması, denetim (c)-14).
/// </summary>
public sealed class MediaService(
    AppDbContext db,
    IFileStorage storage,
    ITenantContext tenant,
    IOptions<StorageOptions> storageOptions) : IMediaService
{
    private const int ThumbnailMaxSize = 320;

    /// <summary>Kullanıcı yüklemesine açık kategoriler; diğerleri (onam PDF'i, imza, fatura) sistem üretir.</summary>
    private static readonly HashSet<MediaCategory> UploadableCategories =
        [MediaCategory.Xray, MediaCategory.IntraoralPhoto, MediaCategory.Document, MediaCategory.LabAttachment];

    /// <summary>İzinli içerik türleri ve uzantıları (jpg/png/webp/pdf).</summary>
    private static readonly Dictionary<string, string[]> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"],
        ["image/png"] = [".png"],
        ["image/webp"] = [".webp"],
        ["application/pdf"] = [".pdf"],
    };

    public async Task<MediaFileDto> UploadAsync(MediaUploadRequest request, CancellationToken ct = default)
    {
        if (!UploadableCategories.Contains(request.Category))
            throw new InvalidOperationException("Bu kategoriye elle yükleme yapılamaz.");
        if (request.SizeBytes <= 0)
            throw new InvalidOperationException("Dosya boş.");
        if (request.SizeBytes > storageOptions.Value.MaxUploadBytes)
            throw new InvalidOperationException(
                $"Dosya boyutu üst sınırı aşıyor ({storageOptions.Value.MaxUploadBytes / (1024 * 1024)} MB).");
        if (!AllowedContentTypes.TryGetValue(request.ContentType, out var extensions))
            throw new InvalidOperationException("Yalnız jpg, png, webp ve pdf dosyaları yüklenebilir.");
        var extension = Path.GetExtension(request.FileName);
        if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Dosya uzantısı içerik türüyle uyuşmuyor ({request.ContentType}).");
        if (request.ToothNumber is not null && !FdiTeeth.IsValid(request.ToothNumber))
            throw new InvalidOperationException("Geçersiz FDI diş numarası.");

        var patient = await db.Patients.AsNoTracking()
                          .FirstOrDefaultAsync(p => p.Id == request.PatientId, ct)
                      ?? throw new KeyNotFoundException("Hasta bulunamadı.");

        // 25 MB üst sınırlı: SHA-256 + thumbnail + depoya yazma tek okuma için bellekte tamponlanır.
        using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.LongLength > storageOptions.Value.MaxUploadBytes)
            throw new InvalidOperationException("Dosya boyutu üst sınırı aşıyor.");

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var storageKey = await storage.SaveAsync(patient.TenantId, request.Category.ToString(),
            request.FileName, new MemoryStream(bytes, writable: false), ct);

        string? thumbnailKey = null;
        if (request.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            thumbnailKey = await CreateThumbnailAsync(patient.TenantId, request.Category, bytes, ct);

        var media = new MediaFile
        {
            ClinicId = patient.ClinicId,
            PatientId = patient.Id,
            Category = request.Category,
            FileName = Path.GetFileName(request.FileName),
            ContentType = request.ContentType,
            SizeBytes = bytes.LongLength,
            StorageKey = storageKey,
            Sha256 = sha256,
            ThumbnailKey = thumbnailKey,
            TakenAt = request.TakenAt,
            Description = request.Description,
            ToothNumber = request.ToothNumber?.Trim(),
            UploadedByUserId = tenant.UserId,
        };
        db.MediaFiles.Add(media);
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(media.Id, ct);
    }

    public async Task<MediaFileDto> SaveGeneratedAsync(GeneratedFileRequest request, CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan dosya kaydedilemez.");

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(request.Content));
        var storageKey = await storage.SaveAsync(tenantId, request.Category.ToString(),
            request.FileName, new MemoryStream(request.Content, writable: false), ct);

        var media = new MediaFile
        {
            ClinicId = request.ClinicId,
            PatientId = request.PatientId,
            Category = request.Category,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.Content.LongLength,
            StorageKey = storageKey,
            Sha256 = sha256,
            Description = request.Description,
            UploadedByUserId = tenant.UserId,
        };
        db.MediaFiles.Add(media);
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(media.Id, ct);
    }

    public async Task<IReadOnlyList<MediaFileDto>> ListForPatientAsync(
        long patientId, MediaCategory? category, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");

        var query = db.MediaFiles.AsNoTracking().Where(m => m.PatientId == patientId);
        if (category is { } c) query = query.Where(m => m.Category == c);
        return await Project(query.OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id)).ToListAsync(ct);
    }

    public async Task<MediaDownload> OpenDownloadAsync(long id, CancellationToken ct = default)
    {
        var media = await FindAsync(id, ct);
        var stream = await storage.OpenReadAsync(media.StorageKey, ct);
        return new MediaDownload(stream, media.FileName, media.ContentType);
    }

    public async Task<MediaDownload> OpenThumbnailAsync(long id, CancellationToken ct = default)
    {
        var media = await FindAsync(id, ct);
        if (media.ThumbnailKey is null)
            throw new KeyNotFoundException("Bu dosyanın küçük resmi yok.");
        var stream = await storage.OpenReadAsync(media.ThumbnailKey, ct);
        return new MediaDownload(stream, $"thumb-{media.Id}.jpg", "image/jpeg");
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var media = await db.MediaFiles.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException("Dosya bulunamadı.");
        // Interceptor soft delete'e çevirir; fiziksel dosya yasal iz için diskte kalır
        // (kalıcı temizlik ileriki retention job'ının işi).
        db.MediaFiles.Remove(media);
        await db.SaveChangesAsync(ct);
    }

    // ---- Yardımcılar ----

    private async Task<MediaFile> FindAsync(long id, CancellationToken ct) =>
        await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException("Dosya bulunamadı.");

    private async Task<string?> CreateThumbnailAsync(
        long tenantId, MediaCategory category, byte[] bytes, CancellationToken ct)
    {
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(bytes);
            if (image.Width > ThumbnailMaxSize || image.Height > ThumbnailMaxSize)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(ThumbnailMaxSize, ThumbnailMaxSize),
                }));
            }
            using var thumbStream = new MemoryStream();
            await image.SaveAsJpegAsync(thumbStream, ct);
            thumbStream.Position = 0;
            return await storage.SaveAsync(tenantId, category.ToString(), "thumbnail.jpg", thumbStream, ct);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new InvalidOperationException("Görüntü dosyası çözümlenemedi (bozuk veya desteklenmeyen biçim).");
        }
    }

    private IQueryable<MediaFileDto> Project(IQueryable<MediaFile> source) =>
        from m in source
        join u in db.Users on m.UploadedByUserId equals (long?)u.Id into uj
        from u in uj.DefaultIfEmpty()
        select new MediaFileDto(
            m.Id, m.PatientId, m.Category, m.FileName, m.ContentType, m.SizeBytes, m.Sha256,
            m.ThumbnailKey != null, m.TakenAt, m.Description, m.ToothNumber,
            m.UploadedByUserId, u != null ? u.FirstName + " " + u.LastName : null, m.CreatedAtUtc);

    private async Task<MediaFileDto> GetDtoAsync(long id, CancellationToken ct) =>
        await Project(db.MediaFiles.AsNoTracking().Where(m => m.Id == id)).FirstAsync(ct);
}
