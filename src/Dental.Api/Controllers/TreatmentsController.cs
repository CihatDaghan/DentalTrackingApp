using Dental.Api.Auth;
using Dental.Application.Treatments;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class TreatmentsController(ITreatmentService treatments) : ControllerBase
{
    /// <summary>Diş şeması render verisi: ToothStatus + aktif tedavi işaretleri.</summary>
    [HttpGet("patients/{patientId:long}/teeth")]
    [HasPermission("treatment.read")]
    public async Task<ActionResult<PatientTeethDto>> GetTeeth(long patientId, CancellationToken ct)
        => Ok(await treatments.GetPatientTeethAsync(patientId, ct));

    /// <summary>Tedavi geçmişi (status filtresi: Diagnosis/Planned/Done/Cancelled).</summary>
    [HttpGet("patients/{patientId:long}/treatments")]
    [HasPermission("treatment.read")]
    public async Task<ActionResult<IReadOnlyList<TreatmentRecordDto>>> List(
        long patientId, [FromQuery] TreatmentRecordStatus? status, CancellationToken ct)
        => Ok(await treatments.ListTreatmentsAsync(patientId, status, ct));

    /// <summary>Toplu tedavi ekleme (diş şemasından çoklu seçim).</summary>
    [HttpPost("patients/{patientId:long}/treatments")]
    [HasPermission("treatment.create")]
    public async Task<ActionResult<IReadOnlyList<TreatmentRecordDto>>> Add(
        long patientId, TreatmentAddRequest request, CancellationToken ct)
        => Ok(await treatments.AddTreatmentsAsync(patientId, request, ct));

    [HttpPut("treatments/{id:long}")]
    [HasPermission("treatment.update")]
    public async Task<ActionResult<TreatmentRecordDto>> Update(long id, TreatmentUpdateRequest request, CancellationToken ct)
        => Ok(await treatments.UpdateAsync(id, request, ct));

    /// <summary>Durum geçişi; Planned→Done'da PerformedAtUtc + ToothStatus etkisi işlenir.</summary>
    [HttpPut("treatments/{id:long}/status")]
    [HasPermission("treatment.update")]
    public async Task<ActionResult<TreatmentRecordDto>> UpdateStatus(long id, TreatmentStatusRequest request, CancellationToken ct)
        => Ok(await treatments.UpdateStatusAsync(id, request, ct));

    /// <summary>"Edit Price": toplu fiyat/indirim güncellemesi.</summary>
    [HttpPut("treatments/bulk-price")]
    [HasPermission("treatment.edit-price")]
    public async Task<ActionResult<IReadOnlyList<TreatmentRecordDto>>> BulkPrice(
        TreatmentBulkPriceRequest request, CancellationToken ct)
        => Ok(await treatments.BulkUpdatePricesAsync(request, ct));

    /// <summary>Done kayıtlar silinemez; Diagnosis/Planned silinebilir.</summary>
    [HttpDelete("treatments/{id:long}")]
    [HasPermission("treatment.delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await treatments.DeleteAsync(id, ct);
        return NoContent();
    }
}
