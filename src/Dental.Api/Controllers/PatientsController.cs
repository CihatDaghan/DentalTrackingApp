using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Patients;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1/patients")]
public sealed class PatientsController(IPatientService patients) : ControllerBase
{
    [HttpGet]
    [HasPermission("patient.read")]
    public async Task<ActionResult<PagedResult<PatientListItemDto>>> List(
        [FromQuery] string? search, [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await patients.ListAsync(new PatientListQuery(search, sort, page, pageSize), ct));

    [HttpPost]
    [HasPermission("patient.create")]
    public async Task<ActionResult<PatientDto>> Create(PatientUpsertRequest request, CancellationToken ct)
    {
        var dto = await patients.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet("{id:long}")]
    [HasPermission("patient.read")]
    public async Task<ActionResult<PatientDto>> Get(long id, CancellationToken ct)
        => Ok(await patients.GetAsync(id, ct));

    [HttpPut("{id:long}")]
    [HasPermission("patient.update")]
    public async Task<ActionResult<PatientDto>> Update(long id, PatientUpsertRequest request, CancellationToken ct)
        => Ok(await patients.UpdateAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    [HasPermission("patient.delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await patients.SoftDeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/summary")]
    [HasPermission("patient.read")]
    public async Task<ActionResult<PatientSummaryDto>> Summary(long id, CancellationToken ct)
        => Ok(await patients.GetSummaryAsync(id, ct));
}
