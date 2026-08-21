using Dental.Api.Auth;
using Dental.Application.Consents;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ConsentsController(IConsentService consents) : ControllerBase
{
    // ---- Şablonlar ----

    [HttpGet("consent-templates")]
    [HasPermission("consent.read")]
    public async Task<ActionResult<IReadOnlyList<ConsentTemplateListItemDto>>> ListTemplates(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await consents.ListTemplatesAsync(includeInactive, ct));

    [HttpGet("consent-templates/{id:long}")]
    [HasPermission("consent.read")]
    public async Task<ActionResult<ConsentTemplateDto>> GetTemplate(long id, CancellationToken ct)
        => Ok(await consents.GetTemplateAsync(id, ct));

    /// <summary>BodyHtml sunucu tarafında sanitize edilerek saklanır.</summary>
    [HttpPost("consent-templates")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<ConsentTemplateDto>> CreateTemplate(
        ConsentTemplateUpsertRequest request, CancellationToken ct)
    {
        var dto = await consents.CreateTemplateAsync(request, ct);
        return CreatedAtAction(nameof(GetTemplate), new { id = dto.Id }, dto);
    }

    [HttpPut("consent-templates/{id:long}")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<ConsentTemplateDto>> UpdateTemplate(
        long id, ConsentTemplateUpsertRequest request, CancellationToken ct)
        => Ok(await consents.UpdateTemplateAsync(id, request, ct));

    [HttpDelete("consent-templates/{id:long}")]
    [HasPermission("settings.update")]
    public async Task<IActionResult> DeleteTemplate(long id, CancellationToken ct)
    {
        await consents.DeleteTemplateAsync(id, ct);
        return NoContent();
    }

    // ---- Formlar ----

    [HttpGet("patients/{id:long}/consents")]
    [HasPermission("consent.read")]
    public async Task<ActionResult<IReadOnlyList<ConsentFormDto>>> ListForPatient(long id, CancellationToken ct)
        => Ok(await consents.ListForPatientAsync(id, ct));

    /// <summary>Şablon + hasta (+ opsiyonel tedavi) → yer tutucular dolu RenderedHtml, Status=Draft.</summary>
    [HttpPost("patients/{id:long}/consents")]
    [HasPermission("consent.create")]
    public async Task<ActionResult<ConsentFormDto>> Create(
        long id, ConsentCreateRequest request, CancellationToken ct)
    {
        var dto = await consents.CreateAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet("consents/{id:long}")]
    [HasPermission("consent.read")]
    public async Task<ActionResult<ConsentFormDto>> Get(long id, CancellationToken ct)
        => Ok(await consents.GetAsync(id, ct));

    /// <summary>Klinik içi tablet imzası: imza PNG (base64) → MediaFile → PDF → Status=Signed.</summary>
    [HttpPost("consents/{id:long}/sign")]
    [HasPermission("consent.create")]
    public async Task<ActionResult<ConsentFormDto>> SignTablet(
        long id, ConsentSignRequest request, CancellationToken ct)
        => Ok(await consents.SignTabletAsync(id, request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(), ct));

    /// <summary>SignToken yenilenir (72 saat) ve public imza linki SMS ile gönderilir.</summary>
    [HttpPost("consents/{id:long}/send-sms")]
    [HasPermission("consent.send")]
    public async Task<ActionResult<ConsentSendSmsResult>> SendSms(long id, CancellationToken ct)
        => Ok(await consents.SendSmsAsync(id, ct));

    [HttpGet("consents/{id:long}/pdf")]
    [HasPermission("consent.read")]
    public async Task<IActionResult> Pdf(long id, CancellationToken ct)
    {
        var download = await consents.OpenPdfAsync(id, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }
}
