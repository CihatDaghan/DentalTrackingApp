using Dental.Api.Auth;
using Dental.Application.Appointments;
using Dental.Application.Common;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1/recalls")]
public sealed class RecallsController(IRecallService recalls) : ControllerBase
{
    [HttpGet]
    [HasPermission("recall.read")]
    public async Task<ActionResult<PagedResult<RecallDto>>> List(
        [FromQuery] long? patientId, [FromQuery] RecallStatus? status, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await recalls.ListAsync(new RecallListQuery(patientId, status, from, to, page, pageSize), ct));

    [HttpPost]
    [HasPermission("recall.create")]
    public async Task<ActionResult<RecallDto>> Create(RecallCreateRequest request, CancellationToken ct)
        => Ok(await recalls.CreateAsync(request, ct));

    [HttpPut("{id:long}/status")]
    [HasPermission("recall.update")]
    public async Task<ActionResult<RecallDto>> UpdateStatus(long id, RecallStatusRequest request, CancellationToken ct)
        => Ok(await recalls.UpdateStatusAsync(id, request, ct));

    [HttpPost("{id:long}/convert")]
    [HasPermission("recall.update")]
    public async Task<ActionResult<RecallDto>> Convert(long id, RecallConvertRequest request, CancellationToken ct)
        => Ok(await recalls.ConvertToAppointmentAsync(id, request, ct));
}
