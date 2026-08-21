using Dental.Application.Common;

namespace Dental.Application.Reports;

/// <summary>
/// Yönetim raporları. Yeni tablo YOKTUR — tümü mevcut işlem verisinden tek sorguda toplanır
/// (AsNoTracking, N+1 yok). Tarih gruplaması Türkiye yerel günü üzerinden yapılır.
/// </summary>
public interface IReportService
{
    Task<RevenueReportDto> GetRevenueAsync(ReportQuery query, CancellationToken ct = default);

    Task<IncomeExpenseReportDto> GetIncomeExpenseAsync(ReportQuery query, CancellationToken ct = default);

    Task<DoctorPerformanceReportDto> GetDoctorPerformanceAsync(ReportQuery query, CancellationToken ct = default);

    Task<CollectionsReportDto> GetCollectionsAsync(ReportQuery query, CancellationToken ct = default);

    Task<TreatmentsReportDto> GetTreatmentsAsync(ReportQuery query, CancellationToken ct = default);

    Task<AppointmentsReportDto> GetAppointmentsAsync(ReportQuery query, CancellationToken ct = default);

    Task<PagedResult<DebtorRowDto>> GetDebtorsAsync(DebtorQuery query, CancellationToken ct = default);

    /// <summary>Jenerik Excel dışa aktarıcı: rapor anahtarı → başlık satırı + veri (TR biçimli).</summary>
    /// <exception cref="KeyNotFoundException">Bilinmeyen rapor anahtarı.</exception>
    Task<ReportFileDto> ExportAsync(
        string reportKey, ReportQuery query, DebtorQuery debtorQuery, CancellationToken ct = default);
}

/// <summary>Gösterge paneli özeti (tek çağrı).</summary>
public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DateOnly? date, long? clinicId, CancellationToken ct = default);
}
