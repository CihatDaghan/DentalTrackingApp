using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>
/// Yönetim raporları. Tüm tarih gruplamaları Türkiye yerel günü üzerindendir;
/// <c>from</c>/<c>to</c> verilmezse son 30 gün alınır.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("revenue")]
    [HasPermission("report.view")]
    public async Task<ActionResult<RevenueReportDto>> Revenue(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] long? doctorId, [FromQuery] long? clinicId,
        [FromQuery] ReportGroupBy groupBy = ReportGroupBy.Day, CancellationToken ct = default)
        => Ok(await reports.GetRevenueAsync(new ReportQuery(from, to, doctorId, clinicId, null, groupBy), ct));

    /// <summary>Aylık gelir/gider serisi + gider kategori kırılımı + net kâr.</summary>
    [HttpGet("income-expense")]
    [HasPermission("report.view")]
    public async Task<ActionResult<IncomeExpenseReportDto>> IncomeExpense(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] long? clinicId,
        CancellationToken ct = default)
        => Ok(await reports.GetIncomeExpenseAsync(new ReportQuery(from, to, null, clinicId), ct));

    [HttpGet("doctor-performance")]
    [HasPermission("report.view")]
    public async Task<ActionResult<DoctorPerformanceReportDto>> DoctorPerformance(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] long? doctorId, [FromQuery] long? clinicId, CancellationToken ct = default)
        => Ok(await reports.GetDoctorPerformanceAsync(new ReportQuery(from, to, doctorId, clinicId), ct));

    /// <summary>Tahsilat dökümü + açık bakiye yaşlandırması (0-30 / 31-60 / 61-90 / 90+ gün).</summary>
    [HttpGet("collections")]
    [HasPermission("report.view")]
    public async Task<ActionResult<CollectionsReportDto>> Collections(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] long? clinicId,
        [FromQuery] ReportGroupBy groupBy = ReportGroupBy.Day, CancellationToken ct = default)
        => Ok(await reports.GetCollectionsAsync(new ReportQuery(from, to, null, clinicId, null, groupBy), ct));

    [HttpGet("treatments")]
    [HasPermission("report.view")]
    public async Task<ActionResult<TreatmentsReportDto>> Treatments(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] long? categoryId, [FromQuery] long? doctorId, [FromQuery] long? clinicId,
        CancellationToken ct = default)
        => Ok(await reports.GetTreatmentsAsync(new ReportQuery(from, to, doctorId, clinicId, categoryId), ct));

    /// <summary>Doluluk, durum dağılımı, gelmeme oranı trendi ve iptal sayıları.</summary>
    [HttpGet("appointments")]
    [HasPermission("report.view")]
    public async Task<ActionResult<AppointmentsReportDto>> Appointments(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] long? doctorId, [FromQuery] long? clinicId,
        [FromQuery] ReportGroupBy groupBy = ReportGroupBy.Day, CancellationToken ct = default)
        => Ok(await reports.GetAppointmentsAsync(new ReportQuery(from, to, doctorId, clinicId, null, groupBy), ct));

    [HttpGet("debtors")]
    [HasPermission("report.view")]
    public async Task<ActionResult<PagedResult<DebtorRowDto>>> Debtors(
        [FromQuery] decimal minBalance = 0.01m,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await reports.GetDebtorsAsync(new DebtorQuery(minBalance, page, pageSize), ct));

    /// <summary>
    /// Jenerik Excel dışa aktarımı. <c>{report}</c>: revenue | income-expense | doctor-performance |
    /// collections | treatments | appointments | debtors.
    /// </summary>
    [HttpGet("{report}/export")]
    [HasPermission("report.export")]
    public async Task<IActionResult> Export(
        string report,
        [FromQuery] string format = "xlsx",
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        [FromQuery] long? doctorId = null, [FromQuery] long? clinicId = null,
        [FromQuery] long? categoryId = null,
        [FromQuery] ReportGroupBy groupBy = ReportGroupBy.Day,
        [FromQuery] decimal minBalance = 0.01m,
        CancellationToken ct = default)
    {
        if (!string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Desteklenen dışa aktarım biçimi: xlsx.");

        var file = await reports.ExportAsync(
            report.ToLowerInvariant(),
            new ReportQuery(from, to, doctorId, clinicId, categoryId, groupBy),
            new DebtorQuery(minBalance),
            ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

/// <summary>Gösterge paneli — ön yüzdeki KPI kartlarının tek kaynağı.</summary>
[ApiController]
[Route("api/v1/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    [HasPermission("report.view")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(
        [FromQuery] DateOnly? date, [FromQuery] long? clinicId, CancellationToken ct)
        => Ok(await dashboard.GetSummaryAsync(date, clinicId, ct));
}
