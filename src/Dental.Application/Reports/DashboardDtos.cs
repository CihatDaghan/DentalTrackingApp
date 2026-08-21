using Dental.Domain.Enums;

namespace Dental.Application.Reports;

/// <summary>Bekleyen iş sayaçları — ön yüzdeki "yapılacaklar" rozetlerini besler.</summary>
public sealed record DashboardPendingWorkDto(
    /// <summary>Teslim tarihi geçmiş, henüz gelmemiş laboratuvar vakaları.</summary>
    int OverdueLabCases,
    /// <summary>CurrentQty &lt;= MinQty olan aktif stok kartları.</summary>
    int LowStockItems,
    /// <summary>Draft/SentBySms durumunda kalmış (imzalanmamış) onam formları.</summary>
    int UnsignedConsents,
    /// <summary>e-Belge hata kuyruğu (Error / ManualReview / GibRejected / BuyerRejected).</summary>
    int EInvoiceErrors,
    /// <summary>Gönderilemeyen mesajlar (Failed).</summary>
    int FailedMessages,
    /// <summary>Bekleyen e-Nabız paketleri (Queued / Held / ManualReview / Rejected).</summary>
    int PendingEnabizPackets);

public sealed record DashboardAppointmentStatusDto(AppointmentStatus Status, int Count);

public sealed record DashboardBirthdayPatientDto(long PatientId, string FullName, string? Phone, int? Age);

public sealed record RevenueTrendPointDto(DateOnly Date, decimal Amount);

/// <summary>
/// Tek çağrılık gösterge paneli özeti. Tüm gün/ay sınırları Türkiye yerel günüdür.
/// Ön yüzdeki KPI kartları ("—" yer tutucular) bu uçtan beslenir.
/// </summary>
public sealed record DashboardSummaryDto(
    DateOnly Date,
    decimal TodayRevenue,
    decimal MonthRevenue,
    decimal TodayCollections,
    decimal MonthCollections,
    decimal TodayExpenses,
    decimal TotalOutstanding,
    int TodayAppointmentCount,
    IReadOnlyList<DashboardAppointmentStatusDto> TodayAppointmentsByStatus,
    DashboardPendingWorkDto PendingWork,
    IReadOnlyList<RevenueTrendPointDto> Last30DaysRevenue,
    IReadOnlyList<DashboardBirthdayPatientDto> BirthdayPatients,
    int ActivePatientCount);
