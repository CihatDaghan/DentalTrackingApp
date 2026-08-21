using Dental.Application.Reports;
using Dental.Domain.Common;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Reports;

/// <summary>
/// Gösterge paneli özeti — ön yüzün tüm KPI kartlarını TEK çağrıda doldurur.
/// Gün/ay sınırları Türkiye yerel günüdür (<see cref="TrTime"/>).
/// </summary>
public sealed class DashboardService(AppDbContext db) : IDashboardService
{
    private static readonly double TrHours = TrTime.Offset.TotalHours;
    private const int TrendDays = 30;
    private const int MaxBirthdayPatients = 20;

    public async Task<DashboardSummaryDto> GetSummaryAsync(
        DateOnly? date, long? clinicId, CancellationToken ct = default)
    {
        var today = date ?? TrTime.ToLocalDate(DateTime.UtcNow);
        var (dayStart, dayEnd) = TrTime.DayRangeUtc(today);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthStartUtc = TrTime.DayRangeUtc(monthStart).StartUtc;
        var trendStart = today.AddDays(-(TrendDays - 1));
        var trendStartUtc = TrTime.DayRangeUtc(trendStart).StartUtc;

        // ---- Ciro (Done tedavi) ----
        var treatments = db.TreatmentRecords.AsNoTracking()
            .Where(t => t.Status == TreatmentRecordStatus.Done && t.PerformedAtUtc != null);
        if (clinicId is { } tc) treatments = treatments.Where(t => t.ClinicId == tc);

        var revenueByDay = await treatments
            .Where(t => t.PerformedAtUtc >= trendStartUtc && t.PerformedAtUtc < dayEnd)
            .GroupBy(t => t.PerformedAtUtc!.Value.AddHours(TrHours).Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(t => t.Price - t.DiscountAmount) })
            .ToListAsync(ct);

        // Ay başı 30 günlük pencerenin dışında kalabilir; aylık toplam ayrı hesaplanır.
        var monthRevenue = await treatments
            .Where(t => t.PerformedAtUtc >= monthStartUtc && t.PerformedAtUtc < dayEnd)
            .SumAsync(t => t.Price - t.DiscountAmount, ct);

        var revenueMap = revenueByDay.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Total);
        var todayRevenue = revenueMap.GetValueOrDefault(today);
        var trend = Enumerable.Range(0, TrendDays)
            .Select(i => trendStart.AddDays(i))
            .Select(d => new RevenueTrendPointDto(d, revenueMap.GetValueOrDefault(d)))
            .ToList();

        // ---- Tahsilat ----
        var payments = db.Payments.AsNoTracking();
        if (clinicId is { } pc) payments = payments.Where(p => p.ClinicId == pc);

        var todayCollections = await payments
            .Where(p => p.ReceivedAtUtc >= dayStart && p.ReceivedAtUtc < dayEnd)
            .SumAsync(p => p.Amount, ct);
        var monthCollections = await payments
            .Where(p => p.ReceivedAtUtc >= monthStartUtc && p.ReceivedAtUtc < dayEnd)
            .SumAsync(p => p.Amount, ct);

        // ---- Gider ----
        var expenses = db.Expenses.AsNoTracking().Where(e => e.ExpenseDate == today);
        if (clinicId is { } ec) expenses = expenses.Where(e => e.ClinicId == ec);
        var todayExpenses = await expenses.SumAsync(e => e.Amount, ct);

        // ---- Açık bakiye (hasta carileri) ----
        var debtors = db.Patients.AsNoTracking().Where(p => p.Balance > 0m);
        if (clinicId is { } dc) debtors = debtors.Where(p => p.ClinicId == dc);
        var totalOutstanding = await debtors.SumAsync(p => p.Balance, ct);

        // ---- Bugünün randevuları ----
        var appointments = db.Appointments.AsNoTracking()
            .Where(a => a.StartUtc >= dayStart && a.StartUtc < dayEnd);
        if (clinicId is { } ac) appointments = appointments.Where(a => a.ClinicId == ac);
        // Gruplama sonucunu doğrudan pozisyonel record'a projekte etmek EF Core'da çevrilemiyor.
        var statusRows = await appointments
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byStatus = statusRows
            .OrderBy(s => s.Status)
            .Select(s => new DashboardAppointmentStatusDto(s.Status, s.Count))
            .ToList();

        // ---- Bekleyen işler ----
        var pending = new DashboardPendingWorkDto(
            OverdueLabCases: await db.LabCases.AsNoTracking()
                .CountAsync(c => c.DueDate != null && c.DueDate < today && c.Status < LabCaseStatus.Received, ct),
            LowStockItems: await db.StockItems.AsNoTracking()
                .CountAsync(i => i.IsActive && i.CurrentQty <= i.MinQty, ct),
            UnsignedConsents: await db.ConsentForms.AsNoTracking()
                .CountAsync(f => f.Status == ConsentFormStatus.Draft || f.Status == ConsentFormStatus.SentBySms, ct),
            EInvoiceErrors: await db.Invoices.AsNoTracking()
                .CountAsync(i => i.Status == InvoiceStatus.Error || i.Status == InvoiceStatus.ManualReview
                                 || i.Status == InvoiceStatus.GibRejected || i.Status == InvoiceStatus.BuyerRejected, ct),
            FailedMessages: await db.OutboundMessages.AsNoTracking()
                .CountAsync(m => m.State == OutboundMessageState.Failed, ct),
            PendingEnabizPackets: await db.EnabizSubmissions.AsNoTracking()
                .CountAsync(s => s.State == EnabizSubmissionState.Queued || s.State == EnabizSubmissionState.Held
                                 || s.State == EnabizSubmissionState.ManualReview
                                 || s.State == EnabizSubmissionState.Rejected, ct));

        // ---- Bugün doğum günü olan hastalar ----
        var birthdayQuery = db.Patients.AsNoTracking()
            .Where(p => p.BirthDate != null
                        && p.BirthDate!.Value.Month == today.Month
                        && p.BirthDate!.Value.Day == today.Day);
        if (clinicId is { } bc) birthdayQuery = birthdayQuery.Where(p => p.ClinicId == bc);
        var birthdays = await birthdayQuery
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Take(MaxBirthdayPatients)
            .Select(p => new { p.Id, p.FirstName, p.LastName, p.Phone, p.BirthDate })
            .ToListAsync(ct);

        var patientQuery = db.Patients.AsNoTracking();
        if (clinicId is { } apc) patientQuery = patientQuery.Where(p => p.ClinicId == apc);
        var activePatients = await patientQuery.CountAsync(ct);

        return new DashboardSummaryDto(
            today,
            todayRevenue, monthRevenue, todayCollections, monthCollections, todayExpenses,
            totalOutstanding,
            byStatus.Sum(s => s.Count), byStatus,
            pending,
            trend,
            [.. birthdays.Select(b => new DashboardBirthdayPatientDto(
                b.Id, $"{b.FirstName} {b.LastName}", b.Phone, Age(b.BirthDate, today)))],
            activePatients);
    }

    private static int? Age(DateOnly? birthDate, DateOnly today)
    {
        if (birthDate is not { } birth) return null;
        var age = today.Year - birth.Year;
        if (today < birth.AddYears(age)) age--;
        return age < 0 ? null : age;
    }
}
