using Dental.Api.Auth;
using Dental.Application.Prescriptions;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class PrescriptionsController(IPrescriptionService prescriptions) : ControllerBase
{
    // ---- İlaçlar (merkezi liste + kiracı özel satırlar) ----

    /// <summary>Ad/barkod araması; merkezi liste (TenantId NULL) + kiracının kendi ilaçları birlikte döner.</summary>
    [HttpGet("drugs")]
    [HasPermission("prescription.read")]
    public async Task<ActionResult<IReadOnlyList<DrugDto>>> SearchDrugs(
        [FromQuery] string? search, CancellationToken ct)
        => Ok(await prescriptions.SearchDrugsAsync(search, ct));

    [HttpPost("drugs")]
    [HasPermission("prescription.create")]
    public async Task<ActionResult<DrugDto>> CreateDrug(DrugCreateRequest request, CancellationToken ct)
        => Ok(await prescriptions.CreateDrugAsync(request, ct));

    // ---- Şablonlar ----

    [HttpGet("prescription-templates")]
    [HasPermission("prescription.read")]
    public async Task<ActionResult<IReadOnlyList<PrescriptionTemplateDto>>> ListTemplates(CancellationToken ct)
        => Ok(await prescriptions.ListTemplatesAsync(ct));

    [HttpGet("prescription-templates/{id:long}")]
    [HasPermission("prescription.read")]
    public async Task<ActionResult<PrescriptionTemplateDto>> GetTemplate(long id, CancellationToken ct)
        => Ok(await prescriptions.GetTemplateAsync(id, ct));

    [HttpPost("prescription-templates")]
    [HasPermission("prescription.create")]
    public async Task<ActionResult<PrescriptionTemplateDto>> CreateTemplate(
        PrescriptionTemplateUpsertRequest request, CancellationToken ct)
    {
        var dto = await prescriptions.CreateTemplateAsync(request, ct);
        return CreatedAtAction(nameof(GetTemplate), new { id = dto.Id }, dto);
    }

    [HttpPut("prescription-templates/{id:long}")]
    [HasPermission("prescription.create")]
    public async Task<ActionResult<PrescriptionTemplateDto>> UpdateTemplate(
        long id, PrescriptionTemplateUpsertRequest request, CancellationToken ct)
        => Ok(await prescriptions.UpdateTemplateAsync(id, request, ct));

    [HttpDelete("prescription-templates/{id:long}")]
    [HasPermission("prescription.create")]
    public async Task<IActionResult> DeleteTemplate(long id, CancellationToken ct)
    {
        await prescriptions.DeleteTemplateAsync(id, ct);
        return NoContent();
    }

    // ---- Reçeteler ----

    [HttpGet("patients/{id:long}/prescriptions")]
    [HasPermission("prescription.read")]
    public async Task<ActionResult<IReadOnlyList<PrescriptionDto>>> ListForPatient(long id, CancellationToken ct)
        => Ok(await prescriptions.ListForPatientAsync(id, ct));

    /// <summary>Hekim (UserType.Dentist) zorunlu. TemplateId verilir ve Items boşsa kalemler şablondan doldurulur.</summary>
    [HttpPost("patients/{id:long}/prescriptions")]
    [HasPermission("prescription.create")]
    public async Task<ActionResult<PrescriptionDto>> Create(
        long id, PrescriptionCreateRequest request, CancellationToken ct)
    {
        var dto = await prescriptions.CreateAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet("prescriptions/{id:long}")]
    [HasPermission("prescription.read")]
    public async Task<ActionResult<PrescriptionDto>> Get(long id, CancellationToken ct)
        => Ok(await prescriptions.GetAsync(id, ct));

    /// <summary>Reçete kalemlerini yeni şablon olarak kaydeder.</summary>
    [HttpPost("prescriptions/{id:long}/save-as-template")]
    [HasPermission("prescription.create")]
    public async Task<ActionResult<PrescriptionTemplateDto>> SaveAsTemplate(
        long id, PrescriptionSaveAsTemplateRequest request, CancellationToken ct)
        => Ok(await prescriptions.SaveAsTemplateAsync(id, request, ct));

    /// <summary>A5 PDF akışı; ilk istekte üretilir ve MediaFile'a yazılır (Status=Printed).</summary>
    [HttpGet("prescriptions/{id:long}/pdf")]
    [HasPermission("prescription.read")]
    public async Task<IActionResult> Pdf(long id, CancellationToken ct)
    {
        var download = await prescriptions.OpenPdfAsync(id, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }
}
