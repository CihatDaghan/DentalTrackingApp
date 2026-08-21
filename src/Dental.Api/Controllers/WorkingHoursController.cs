using Dental.Api.Auth;
using Dental.Application.Appointments;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1/working-hours")]
public sealed class WorkingHoursController(IAppointmentService appointments) : ControllerBase
{
    [HttpGet]
    [HasPermission("appointment.read")]
    public async Task<ActionResult<IReadOnlyList<DoctorWorkingHourDto>>> Get(
        [FromQuery] long? doctorId, CancellationToken ct)
        => Ok(await appointments.GetWorkingHoursAsync(doctorId, ct));

    /// <summary>Hekimin çalışma saatlerini toplu kaydeder (mevcutları değiştirir).</summary>
    [HttpPost]
    [HasPermission("settings.update")]
    public async Task<ActionResult<IReadOnlyList<DoctorWorkingHourDto>>> Save(
        WorkingHoursSaveRequest request, CancellationToken ct)
        => Ok(await appointments.SaveWorkingHoursAsync(request, ct));
}
