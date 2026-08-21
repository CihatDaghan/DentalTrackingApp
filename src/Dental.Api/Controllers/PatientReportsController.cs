using Dental.Api.Auth;
using Dental.Application.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>
/// Hasta kartı "Rapor" sekmesi (tasarım §2.5 madde 11): tedavi dökümü, durum bildirir rapor,
/// proforma. <c>?format=pdf</c> ile klinik antetli PDF döner ve belge MediaFile arşivine yazılır.
/// </summary>
[ApiController]
[Route("api/v1/patients/{id:long}/reports")]
public sealed class PatientReportsController(IPatientReportService reports) : ControllerBase
{
    private const string PdfFormat = "pdf";

    /// <summary>Tarih aralığındaki yapılmış tedavilerin dökümü (tarih, hekim, diş, tedavi, tutar).</summary>
    [HttpGet("treatment")]
    [HasPermission("report.view")]
    public async Task<IActionResult> Treatment(
        long id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? format, CancellationToken ct)
    {
        if (!IsPdf(format)) return Ok(await reports.GetTreatmentReportAsync(id, from, to, ct));

        var (_, file) = await reports.GetTreatmentReportPdfAsync(id, from, to, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Durum bildirir rapor: kimlik bloğu, diş durumu, yapılan tedaviler, hekim imza alanı.</summary>
    [HttpGet("status")]
    [HasPermission("report.view")]
    public async Task<IActionResult> Status(long id, [FromQuery] string? format, CancellationToken ct)
    {
        if (!IsPdf(format)) return Ok(await reports.GetStatusReportAsync(id, ct));

        var (_, file) = await reports.GetStatusReportPdfAsync(id, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>
    /// Fiyat teklifi (proforma): gövdede seçilen PLANLANMIŞ tedavi kayıtları.
    /// Fatura değildir — mali belge niteliği taşımaz.
    /// </summary>
    [HttpPost("proforma")]
    [HasPermission("report.view")]
    public async Task<IActionResult> Proforma(
        long id, ProformaRequest request, [FromQuery] string? format, CancellationToken ct)
    {
        if (!IsPdf(format)) return Ok(await reports.CreateProformaAsync(id, request, ct));

        var (_, file) = await reports.CreateProformaPdfAsync(id, request, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    private static bool IsPdf(string? format) =>
        string.Equals(format, PdfFormat, StringComparison.OrdinalIgnoreCase);
}
