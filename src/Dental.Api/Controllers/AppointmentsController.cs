using Dental.Api.Auth;
using Dental.Application.Appointments;
using Dental.Domain.Enums;
using Dental.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1/appointments")]
public sealed class AppointmentsController(IAppointmentService appointments) : ControllerBase
{
    [HttpGet]
    [HasPermission("appointment.read")]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> List(
        [FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] long? clinicId, [FromQuery] string? doctorIds, CancellationToken ct)
    {
        var ids = doctorIds?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(long.Parse).ToList();
        return Ok(await appointments.ListAsync(new AppointmentListQuery(from, to, clinicId, ids), ct));
    }

    [HttpPost]
    [HasPermission("appointment.create")]
    public async Task<ActionResult<AppointmentDto>> Create(AppointmentUpsertRequest request, CancellationToken ct)
    {
        var dto = await appointments.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpGet("{id:long}")]
    [HasPermission("appointment.read")]
    public async Task<ActionResult<AppointmentDto>> Get(long id, CancellationToken ct)
        => Ok(await appointments.GetAsync(id, ct));

    [HttpPut("{id:long}")]
    [HasPermission("appointment.update")]
    public async Task<ActionResult<AppointmentDto>> Update(long id, AppointmentUpsertRequest request, CancellationToken ct)
        => Ok(await appointments.UpdateAsync(id, request, ct));

    [HttpPut("{id:long}/status")]
    [HasPermission("appointment.update")]
    public async Task<ActionResult<AppointmentDto>> UpdateStatus(long id, AppointmentStatusRequest request, CancellationToken ct)
    {
        // İptal ayrı izne bağlıdır (appointment.cancel); diğer geçişler appointment.update ile.
        if (request.Status == AppointmentStatus.Cancelled && !UserHasPermission("appointment.cancel"))
            return Forbid();
        return Ok(await appointments.UpdateStatusAsync(id, request, ct));
    }

    [HttpDelete("{id:long}")]
    [HasPermission("appointment.delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await appointments.DeleteAsync(id, ct);
        return NoContent();
    }

    private bool UserHasPermission(string code) =>
        User.FindAll(AppClaimTypes.Permission).Any(c => c.Value == "*" || c.Value == code);
}
