using Dental.Api.Auth;
using Dental.Application.Clinical;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1/patients/{patientId:long}/notes")]
public sealed class PatientNotesController(IPatientNoteService notes) : ControllerBase
{
    [HttpGet]
    [HasPermission("note.read")]
    public async Task<ActionResult<IReadOnlyList<PatientNoteDto>>> List(long patientId, CancellationToken ct)
        => Ok(await notes.ListAsync(patientId, ct));

    [HttpPost]
    [HasPermission("note.create")]
    public async Task<ActionResult<PatientNoteDto>> Create(
        long patientId, PatientNoteUpsertRequest request, CancellationToken ct)
        => Ok(await notes.CreateAsync(patientId, request, ct));

    /// <summary>Yalnız yazar veya Owner/Manager düzenleyebilir (servis kuralı, aksi 403).</summary>
    [HttpPut("{noteId:long}")]
    [HasPermission("note.update")]
    public async Task<ActionResult<PatientNoteDto>> Update(
        long patientId, long noteId, PatientNoteUpsertRequest request, CancellationToken ct)
        => Ok(await notes.UpdateAsync(patientId, noteId, request, ct));

    /// <summary>Yalnız yazar veya Owner/Manager silebilir (servis kuralı, aksi 403).</summary>
    [HttpDelete("{noteId:long}")]
    [HasPermission("note.delete")]
    public async Task<IActionResult> Delete(long patientId, long noteId, CancellationToken ct)
    {
        await notes.DeleteAsync(patientId, noteId, ct);
        return NoContent();
    }
}
