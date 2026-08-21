using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Invoices;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>
/// e-Belge uçları. Akış: preview (karar motoru) → create (Draft) → generate-ubl (numara + ETTN)
/// → send (kuyruk/gönderim) → status/pdf → cancel.
/// </summary>
[ApiController]
[Route("api/v1/invoices")]
public sealed class InvoicesController(IInvoiceService invoices) : ControllerBase
{
    /// <summary>
    /// Yan etkisiz taslak: belge tipi kararı + gerekçe + satırlar + toplamlar + eksik alan uyarıları.
    /// <c>canCreate=false</c> ise <c>errors</c> düzeltilmeden fatura oluşturulamaz.
    /// </summary>
    [HttpPost("preview")]
    [HasPermission("invoice.create")]
    public async Task<ActionResult<InvoicePreviewDto>> Preview(InvoiceDraftRequest request, CancellationToken ct)
        => Ok(await invoices.PreviewAsync(request, ct));

    [HttpPost]
    [HasPermission("invoice.create")]
    public async Task<ActionResult<InvoiceDto>> Create(InvoiceDraftRequest request, CancellationToken ct)
    {
        var dto = await invoices.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Draft → UblGenerated: atomik belge numarası + ETTN atanır, UBL üretilip arşivlenir.</summary>
    [HttpPost("{id:long}/generate-ubl")]
    [HasPermission("invoice.create")]
    public async Task<ActionResult<InvoiceDto>> GenerateUbl(long id, CancellationToken ct)
        => Ok(await invoices.GenerateUblAsync(id, ct));

    /// <summary>
    /// UblGenerated → Queued. <c>sendNow=true</c> (varsayılan) entegratöre hemen gönderir;
    /// false ise gönderimi EDocumentDispatchJob üstlenir.
    /// </summary>
    [HttpPost("{id:long}/send")]
    [HasPermission("invoice.send")]
    public async Task<ActionResult<InvoiceDto>> Send(long id, [FromQuery] bool sendNow = true, CancellationToken ct = default)
        => Ok(await invoices.SendAsync(id, sendNow, ct));

    [HttpPost("{id:long}/cancel")]
    [HasPermission("invoice.cancel")]
    public async Task<ActionResult<InvoiceDto>> Cancel(long id, InvoiceCancelRequest request, CancellationToken ct)
        => Ok(await invoices.CancelAsync(id, request, ct));

    [HttpGet]
    [HasPermission("invoice.read")]
    public async Task<ActionResult<PagedResult<InvoiceListItemDto>>> List(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => Ok(await invoices.ListAsync(status, from, to, page, pageSize, ct));

    /// <summary>Fatura detayı: satırlar + durum geçiş geçmişi.</summary>
    [HttpGet("{id:long}")]
    [HasPermission("invoice.read")]
    public async Task<ActionResult<InvoiceDto>> Get(long id, CancellationToken ct)
        => Ok(await invoices.GetAsync(id, ct));

    /// <summary>Üretilen UBL-TR 1.2 XML akışı.</summary>
    [HttpGet("{id:long}/ubl")]
    [HasPermission("invoice.read")]
    public async Task<IActionResult> Ubl(long id, CancellationToken ct)
    {
        var download = await invoices.OpenUblAsync(id, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }

    /// <summary>Entegratörden alınan belge PDF'i (ilk istekte indirilip arşivlenir).</summary>
    [HttpGet("{id:long}/pdf")]
    [HasPermission("invoice.read")]
    public async Task<IActionResult> Pdf(long id, CancellationToken ct)
    {
        var download = await invoices.OpenPdfAsync(id, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }
}

/// <summary>GİB mükellef aynası sorgusu — lokal cache'ten (günlük job tazeler).</summary>
[ApiController]
[Route("api/v1/gib-taxpayers")]
public sealed class GibTaxpayersController(IInvoiceService invoices) : ControllerBase
{
    [HttpGet("{vkn}")]
    [HasPermission("invoice.read")]
    public async Task<ActionResult<GibTaxpayerDto>> Get(string vkn, CancellationToken ct)
        => Ok(await invoices.GetTaxpayerAsync(vkn, ct));
}
