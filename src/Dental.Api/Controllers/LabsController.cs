using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Labs;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class LabsController(ILabService labs) : ControllerBase
{
    // ---- Laboratuvar firmaları ----

    [HttpGet("laboratories")]
    [HasPermission("lab.read")]
    public async Task<ActionResult<IReadOnlyList<LaboratoryDto>>> ListLaboratories(CancellationToken ct)
        => Ok(await labs.ListLaboratoriesAsync(ct));

    [HttpGet("laboratories/{id:long}")]
    [HasPermission("lab.read")]
    public async Task<ActionResult<LaboratoryDto>> GetLaboratory(long id, CancellationToken ct)
        => Ok(await labs.GetLaboratoryAsync(id, ct));

    [HttpPost("laboratories")]
    [HasPermission("lab.create")]
    public async Task<ActionResult<LaboratoryDto>> CreateLaboratory(
        LaboratoryUpsertRequest request, CancellationToken ct)
    {
        var dto = await labs.CreateLaboratoryAsync(request, ct);
        return CreatedAtAction(nameof(GetLaboratory), new { id = dto.Id }, dto);
    }

    [HttpPut("laboratories/{id:long}")]
    [HasPermission("lab.update")]
    public async Task<ActionResult<LaboratoryDto>> UpdateLaboratory(
        long id, LaboratoryUpsertRequest request, CancellationToken ct)
        => Ok(await labs.UpdateLaboratoryAsync(id, request, ct));

    /// <summary>Açık vakası olan laboratuvar silinemez.</summary>
    [HttpDelete("laboratories/{id:long}")]
    [HasPermission("lab.delete")]
    public async Task<IActionResult> DeleteLaboratory(long id, CancellationToken ct)
    {
        await labs.DeleteLaboratoryAsync(id, ct);
        return NoContent();
    }

    // ---- Vakalar ----

    /// <summary>Filtreli liste; isOverdue = DueDate &lt; bugün ve Status &lt; Received.</summary>
    [HttpGet("lab-cases")]
    [HasPermission("lab.read")]
    public async Task<ActionResult<PagedResult<LabCaseDto>>> ListCases(
        [FromQuery] LabCaseStatus? status, [FromQuery] long? laboratoryId,
        [FromQuery] long? doctorUserId, [FromQuery] long? patientId,
        [FromQuery] DateOnly? dueFrom, [FromQuery] DateOnly? dueTo,
        [FromQuery] bool overdueOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await labs.ListCasesAsync(new LabCaseListQuery(
            status, laboratoryId, doctorUserId, patientId, dueFrom, dueTo, overdueOnly, page, pageSize), ct));

    [HttpGet("lab-cases/{id:long}")]
    [HasPermission("lab.read")]
    public async Task<ActionResult<LabCaseDto>> GetCase(long id, CancellationToken ct)
        => Ok(await labs.GetCaseAsync(id, ct));

    [HttpPost("lab-cases")]
    [HasPermission("lab.create")]
    public async Task<ActionResult<LabCaseDto>> CreateCase(LabCaseUpsertRequest request, CancellationToken ct)
    {
        var dto = await labs.CreateCaseAsync(request, ct);
        return CreatedAtAction(nameof(GetCase), new { id = dto.Id }, dto);
    }

    [HttpPut("lab-cases/{id:long}")]
    [HasPermission("lab.update")]
    public async Task<ActionResult<LabCaseDto>> UpdateCase(
        long id, LabCaseUpsertRequest request, CancellationToken ct)
        => Ok(await labs.UpdateCaseAsync(id, request, ct));

    /// <summary>Durum geçişi; her geçiş geçmişe yazılır (Sent → SentDate, Received → ReceivedDate).</summary>
    [HttpPut("lab-cases/{id:long}/status")]
    [HasPermission("lab.update")]
    public async Task<ActionResult<LabCaseDto>> ChangeStatus(
        long id, LabCaseStatusChangeRequest request, CancellationToken ct)
        => Ok(await labs.ChangeStatusAsync(id, request, ct));

    [HttpGet("lab-cases/{id:long}/history")]
    [HasPermission("lab.read")]
    public async Task<ActionResult<IReadOnlyList<LabCaseHistoryDto>>> GetHistory(long id, CancellationToken ct)
        => Ok(await labs.GetHistoryAsync(id, ct));

    [HttpDelete("lab-cases/{id:long}")]
    [HasPermission("lab.delete")]
    public async Task<IActionResult> DeleteCase(long id, CancellationToken ct)
    {
        await labs.DeleteCaseAsync(id, ct);
        return NoContent();
    }

    [HttpGet("patients/{id:long}/lab-cases")]
    [HasPermission("lab.read")]
    public async Task<ActionResult<IReadOnlyList<LabCaseDto>>> ListForPatient(long id, CancellationToken ct)
        => Ok(await labs.ListForPatientAsync(id, ct));
}
