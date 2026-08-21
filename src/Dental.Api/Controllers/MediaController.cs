using Dental.Api.Auth;
using Dental.Application.Media;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class MediaController(IMediaService media) : ControllerBase
{
    /// <summary>Multipart form alanları; dosya + kategori zorunlu.</summary>
    public sealed class MediaUploadForm
    {
        public required IFormFile File { get; set; }
        public MediaCategory Category { get; set; }
        public string? Description { get; set; }
        public string? ToothNumber { get; set; }
        public DateOnly? TakenAt { get; set; }
    }

    /// <summary>Hasta medyası yükleme (jpg/png/webp/pdf; üst sınır Storage:MaxUploadBytes, varsayılan 25 MB).</summary>
    [HttpPost("patients/{id:long}/media")]
    [HasPermission("media.upload")]
    [RequestSizeLimit(27_000_000)] // 25 MB dosya + multipart yükü
    public async Task<ActionResult<MediaFileDto>> Upload(
        long id, [FromForm] MediaUploadForm form, CancellationToken ct)
    {
        await using var content = form.File.OpenReadStream();
        var dto = await media.UploadAsync(new MediaUploadRequest(
            id, form.Category, form.File.FileName, form.File.ContentType, form.File.Length,
            content, form.Description, form.ToothNumber, form.TakenAt), ct);
        return CreatedAtAction(nameof(Download), new { id = dto.Id }, dto);
    }

    [HttpGet("patients/{id:long}/media")]
    [HasPermission("media.read")]
    public async Task<ActionResult<IReadOnlyList<MediaFileDto>>> List(
        long id, [FromQuery] MediaCategory? category, CancellationToken ct)
        => Ok(await media.ListForPatientAsync(id, category, ct));

    /// <summary>İçerik stream olarak döner (Content-Disposition: attachment). Tenant doğrulaması global filter'da.</summary>
    [HttpGet("media/{id:long}/download")]
    [HasPermission("media.read")]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        var download = await media.OpenDownloadAsync(id, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HttpGet("media/{id:long}/thumbnail")]
    [HasPermission("media.read")]
    public async Task<IActionResult> Thumbnail(long id, CancellationToken ct)
    {
        var download = await media.OpenThumbnailAsync(id, ct);
        return File(download.Content, download.ContentType);
    }

    [HttpDelete("media/{id:long}")]
    [HasPermission("media.delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await media.DeleteAsync(id, ct);
        return NoContent();
    }
}
