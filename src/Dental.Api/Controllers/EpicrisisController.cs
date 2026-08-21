using Dental.Api.Auth;
using Dental.Application.Epicrisis;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class EpicrisisController(IEpicrisisService epicrisis) : ControllerBase
{
    [HttpGet("patients/{id:long}/epicrisis")]
    [HasPermission("epicrisis.read")]
    public async Task<ActionResult<IReadOnlyList<EpicrisisDto>>> ListForPatient(long id, CancellationToken ct)
        => Ok(await epicrisis.ListForPatientAsync(id, ct));

    /// <summary>
    /// Oluşturur: tedavi id'lerinin özetleri ve ICD tanıları JSON snapshot olarak sabitlenir.
    /// Hekim (UserType.Dentist) zorunlu. ICD araması mevcut /api/v1/icd-codes ucundan yapılır.
    /// </summary>
    [HttpPost("patients/{id:long}/epicrisis")]
    [HasPermission("epicrisis.create")]
    public async Task<ActionResult<EpicrisisDto>> Create(
        long id, EpicrisisCreateRequest request, CancellationToken ct)
    {
        var dto = await epicrisis.CreateAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet("epicrisis/{id:long}")]
    [HasPermission("epicrisis.read")]
    public async Task<ActionResult<EpicrisisDto>> Get(long id, CancellationToken ct)
        => Ok(await epicrisis.GetAsync(id, ct));

    /// <summary>Antetli A4 PDF akışı; ilk istekte üretilir ve MediaFile'a yazılır.</summary>
    [HttpGet("epicrisis/{id:long}/pdf")]
    [HasPermission("epicrisis.read")]
    public async Task<IActionResult> Pdf(long id, CancellationToken ct)
    {
        var download = await epicrisis.OpenPdfAsync(id, ct);
        return File(download.Content, download.ContentType, download.FileName);
    }
}
