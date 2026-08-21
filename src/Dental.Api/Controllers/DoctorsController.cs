using Dental.Api.Auth;
using Dental.Application.Abstractions;
using Dental.Application.Appointments;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dental.Api.Controllers;

/// <summary>Takvim için tenant'ın hekim listesi. AppUser tenant filtresine tabi olmadığından elle filtrelenir.</summary>
[ApiController]
[Route("api/v1/doctors")]
public sealed class DoctorsController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [HasPermission("appointment.read")]
    public async Task<ActionResult<IReadOnlyList<DoctorDto>>> List(CancellationToken ct)
    {
        var doctors = await db.Users
            .Where(u => u.TenantId == tenant.TenantId && u.UserType == UserType.Dentist && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new DoctorDto(u.Id, u.FirstName, u.LastName, u.Color, u.Branch))
            .ToListAsync(ct);
        return Ok(doctors);
    }
}
