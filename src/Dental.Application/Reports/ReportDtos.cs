using Dental.Domain.Enums;

namespace Dental.Application.Reports;

/// <summary>
/// Dönem kırılımı. Gruplama HER ZAMAN Türkiye yerel günü üzerinden yapılır
/// (<c>TrTime</c>); UTC damgalar önce yerel güne çevrilir, sonra kovalanır.
/// </summary>
public enum ReportGroupBy : byte
{
    Day = 1,
    Week = 2,
    Month = 3,
}

/// <summary>Tüm raporların ortak filtresi. From/To dahildir (yerel gün).</summary>
public sealed record ReportQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    long? DoctorId = null,
    long? ClinicId = null,
    long? CategoryId = null,
    ReportGroupBy GroupBy = ReportGroupBy.Day);

/// <summary>Uygulanmış (varsayılanları çözülmüş) tarih aralığı — yanıtlarda geri döner.</summary>
public sealed record ReportPeriodDto(DateOnly From, DateOnly To, ReportGroupBy GroupBy);

// ---- Ciro ----

/// <param name="Period">Dönem başlangıcı (gün/hafta başı pazartesi/ay başı).</param>
public sealed record RevenuePointDto(
    DateOnly Period,
    string PeriodLabel,
    decimal TreatmentRevenue,
    decimal Collected,
    int TreatmentCount);

public sealed record PaymentMethodTotalDto(PaymentMethod Method, decimal Total, int Count);

public sealed record RevenueReportDto(
    ReportPeriodDto Period,
    IReadOnlyList<RevenuePointDto> Series,
    IReadOnlyList<PaymentMethodTotalDto> ByMethod,
    decimal TotalTreatmentRevenue,
    decimal TotalCollected,
    int TotalTreatmentCount);

// ---- Gelir / gider ----

public sealed record IncomeExpensePointDto(
    DateOnly Period,
    string PeriodLabel,
    decimal Income,
    decimal Expense,
    decimal Net);

public sealed record ExpenseCategoryTotalDto(long CategoryId, string CategoryName, decimal Total, int Count);

public sealed record IncomeExpenseReportDto(
    ReportPeriodDto Period,
    IReadOnlyList<IncomeExpensePointDto> Series,
    IReadOnlyList<ExpenseCategoryTotalDto> ExpensesByCategory,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetProfit);

// ---- Hekim performansı ----

public sealed record DoctorPerformanceRowDto(
    long DoctorUserId,
    string DoctorName,
    string? Branch,
    int PatientCount,
    int TreatmentCount,
    /// <summary>Yapılan (Done) tedavilerden üretilen net ciro.</summary>
    decimal ProducedRevenue,
    /// <summary>Hekimin hastalarından dönem içinde tahsil edilen tutar.</summary>
    decimal CollectedRevenue,
    int AppointmentCount,
    int NoShowCount,
    decimal NoShowRate);

public sealed record DoctorPerformanceReportDto(
    ReportPeriodDto Period,
    IReadOnlyList<DoctorPerformanceRowDto> Rows);

// ---- Tahsilat + yaşlandırma ----

public sealed record CollectionPointDto(DateOnly Period, string PeriodLabel, decimal Total, int Count);

/// <param name="Bucket">"0-30" | "31-60" | "61-90" | "90+"</param>
public sealed record AgingBucketDto(string Bucket, decimal Amount, int PatientCount);

public sealed record CollectionsReportDto(
    ReportPeriodDto Period,
    IReadOnlyList<CollectionPointDto> Series,
    IReadOnlyList<PaymentMethodTotalDto> ByMethod,
    decimal TotalCollected,
    int TotalCount,
    IReadOnlyList<AgingBucketDto> Aging,
    decimal TotalOutstanding);

// ---- Tedavi dökümü ----

public sealed record TreatmentReportRowDto(
    long TreatmentDefinitionId,
    string Code,
    string Name,
    long CategoryId,
    string CategoryName,
    int Count,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount);

public sealed record TreatmentCategoryTotalDto(long CategoryId, string CategoryName, int Count, decimal NetAmount);

public sealed record TreatmentsReportDto(
    ReportPeriodDto Period,
    IReadOnlyList<TreatmentReportRowDto> Rows,
    IReadOnlyList<TreatmentCategoryTotalDto> ByCategory,
    int TotalCount,
    decimal TotalNetAmount);

// ---- Randevu ----

public sealed record AppointmentStatusTotalDto(AppointmentStatus Status, int Count);

public sealed record AppointmentTrendPointDto(
    DateOnly Period,
    string PeriodLabel,
    int Total,
    int Completed,
    int NoShow,
    int Cancelled,
    decimal NoShowRate,
    int BookedMinutes,
    int CapacityMinutes,
    decimal OccupancyRate);

public sealed record AppointmentsReportDto(
    ReportPeriodDto Period,
    IReadOnlyList<AppointmentStatusTotalDto> ByStatus,
    IReadOnlyList<AppointmentTrendPointDto> Trend,
    int TotalCount,
    int NoShowCount,
    int CancelledCount,
    decimal NoShowRate,
    decimal OccupancyRate);

// ---- Borçlu hastalar ----

public sealed record DebtorRowDto(
    long PatientId,
    string FileNo,
    string FullName,
    string? Phone,
    decimal Balance,
    DateOnly? LastEntryDate,
    DateTime? LastAppointmentUtc);

public sealed record DebtorQuery(decimal MinBalance = 0.01m, int Page = 1, int PageSize = 25);

// ---- Excel dışa aktarım ----

/// <summary>Desteklenen rapor anahtarları (dışa aktarım rotasındaki {report} segmenti).</summary>
public static class ReportKeys
{
    public const string Revenue = "revenue";
    public const string IncomeExpense = "income-expense";
    public const string DoctorPerformance = "doctor-performance";
    public const string Collections = "collections";
    public const string Treatments = "treatments";
    public const string Appointments = "appointments";
    public const string Debtors = "debtors";

    public static readonly IReadOnlyList<string> All =
        [Revenue, IncomeExpense, DoctorPerformance, Collections, Treatments, Appointments, Debtors];
}

/// <summary>Üretilmiş dosya (Excel/PDF) — controller FileContentResult ile döner.</summary>
public sealed record ReportFileDto(byte[] Content, string FileName, string ContentType);
