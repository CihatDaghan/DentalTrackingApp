using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Enabiz;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>
/// e-Nabız / USS uçları. Akış: tedavi Done → paket kuyruğa girer (mod Held ise bekler) →
/// dispatcher gönderir → 101 kabul + SysTakipNo → bağımlı paketler (102/103/203) gider.
/// </summary>
[ApiController]
[Route("api/v1/enabiz")]
public sealed class EnabizController(
    IEnabizService enabiz,
    IEnabizDispatcher dispatcher,
    IEnabizSubmissionQueue queue) : ControllerBase
{
    /// <summary>Gönderim kuyruğu listesi (durum / paket tipi / tarih aralığı filtreli).</summary>
    [HttpGet("submissions")]
    [HasPermission("enabiz.read")]
    public async Task<ActionResult<PagedResult<EnabizSubmissionListItemDto>>> List(
        [FromQuery] EnabizSubmissionState? state,
        [FromQuery] EnabizPacketType? packetType,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => Ok(await enabiz.ListAsync(state, packetType, from, to, page, pageSize, ct));

    /// <summary>Gönderim detayı — üretilen paket XML'i dahil (destek/denetim).</summary>
    [HttpGet("submissions/{id:long}")]
    [HasPermission("enabiz.read")]
    public async Task<ActionResult<EnabizSubmissionDto>> Get(long id, CancellationToken ct)
        => Ok(await enabiz.GetAsync(id, ct));

    /// <summary>Reddedilmiş/elle incelemedeki paketi yeniden kuyruğa alıp gönderir.</summary>
    [HttpPost("submissions/{id:long}/retry")]
    [HasPermission("enabiz.send")]
    public async Task<ActionResult<EnabizSubmissionDto>> Retry(long id, CancellationToken ct)
    {
        await dispatcher.RetryAsync(id, ct);
        return Ok(await enabiz.GetAsync(id, ct));
    }

    /// <summary>Bir ziyaretin paketlerini üretip kuyruğa alır ve (mod uygunsa) hemen gönderir.</summary>
    [HttpPost("visits/{visitId:long}/send")]
    [HasPermission("enabiz.send")]
    public async Task<ActionResult<EnabizQueueResultDto>> SendVisit(long visitId, CancellationToken ct)
    {
        var result = await queue.QueueVisitAsync(visitId, ct);
        foreach (var id in result.SubmissionIds)
            await dispatcher.DispatchAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Dashboard/ayarlar özeti: mod, bekleyen paket sayıları, son senkron, KTS tescili.</summary>
    [HttpGet("status")]
    [HasPermission("enabiz.read")]
    public async Task<ActionResult<EnabizStatusDto>> Status(CancellationToken ct)
        => Ok(await enabiz.GetStatusAsync(ct));

    [HttpGet("settings")]
    [HasPermission("enabiz.manage")]
    public async Task<ActionResult<EnabizSettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await enabiz.GetSettingsAsync(ct));

    /// <summary>
    /// ÇKYS kodu, USS kimliği ve mod ayarı. Live moda geçiş sistem düzeyi KTS tescil bayrağına bağlıdır.
    /// </summary>
    [HttpPut("settings")]
    [HasPermission("enabiz.manage")]
    public async Task<ActionResult<EnabizSettingsDto>> UpdateSettings(
        EnabizSettingsRequest request, CancellationToken ct)
        => Ok(await enabiz.UpdateSettingsAsync(request, ct));
}

/// <summary>SKRS kod seti sorgusu — tanı/işlem kodu seçim ekranları için.</summary>
[ApiController]
[Route("api/v1/skrs")]
public sealed class SkrsController(ISkrsCodeService codes) : ControllerBase
{
    [HttpGet("codes")]
    [HasPermission("treatment.read")]
    public async Task<ActionResult<IReadOnlyList<SkrsCodeDto>>> Search(
        [FromQuery] string? systemName,
        [FromQuery] string? search,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
        => Ok(await codes.SearchAsync(systemName, search, limit, ct));
}
