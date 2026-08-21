using Dental.Application.Common;
using Dental.Application.Reports;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Reports;

/// <summary>
/// Yönetim raporları. Yeni tablo yoktur; her rapor mevcut işlem verisinden okur.
///
/// <para><b>TR yerel günü:</b> tüm tarih kovaları Europe/Istanbul (sabit UTC+3) yerel günüdür.
/// Gruplama SQL tarafında <c>CONVERT(date, DATEADD(hour, 3, ...))</c> olarak çevrilir; hafta/ay
/// kırılımı bu günlük kovaların üzerine bellekte katlanır (ikinci sorgu açılmaz).</para>
///
/// <para><b>N+1 yok:</b> her rapor sabit sayıda (1-4) sorgu açar; satır başına sorgu yoktur.
/// Tüm okumalar <c>AsNoTracking</c>.</para>
/// </summary>
public sealed class ReportService(AppDbContext db) : IReportService
{
    /// <summary>Türkiye sabit UTC farkı — SQL'e DATEADD(hour, 3, ...) olarak çevrilir.</summary>
    private static readonly double TrHours = TrTime.Offset.TotalHours;

    /// <summary>Filtre verilmezse son 30 gün.</summary>
    private const int DefaultRangeDays = 29;

    // ---- Ciro ----

    public async Task<RevenueReportDto> GetRevenueAsync(ReportQuery query, CancellationToken ct = default)
    {
        var (from, to, startUtc, endUtc) = Resolve(query);

        var treatments = FilterTreatments(query, startUtc, endUtc);
        var treatmentSeries = await treatments
            .GroupBy(t => t.PerformedAtUtc!.Value.AddHours(TrHours).Date)
            .Select(g => new
            {
                Day = g.Key,
                Revenue = g.Sum(t => t.Price - t.DiscountAmount),
                Count = g.Count(),
            })
            .ToListAsync(ct);

        var payments = FilterPayments(query, startUtc, endUtc);
        var paymentSeries = await payments
            .GroupBy(p => p.ReceivedAtUtc.AddHours(TrHours).Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var byMethod = await LoadMethodTotalsAsync(payments, ct);

        var treatmentBuckets = Fold(treatmentSeries, x => x.Day, query.GroupBy);
        var paymentBuckets = Fold(paymentSeries, x => x.Day, query.GroupBy);

        var series = EnumeratePeriods(from, to, query.GroupBy)
            .Select(period => new RevenuePointDto(
                period,
                Label(period, query.GroupBy),
                treatmentBuckets.TryGetValue(period, out var tr) ? tr.Sum(x => x.Revenue) : 0m,
                paymentBuckets.TryGetValue(period, out var pay) ? pay.Sum(x => x.Total) : 0m,
                treatmentBuckets.TryGetValue(period, out var trc) ? trc.Sum(x => x.Count) : 0))
            .ToList();

        return new RevenueReportDto(
            new ReportPeriodDto(from, to, query.GroupBy),
            series,
            byMethod,
            treatmentSeries.Sum(x => x.Revenue),
            paymentSeries.Sum(x => x.Total),
            treatmentSeries.Sum(x => x.Count));
    }

    // ---- Gelir / gider ----

    public async Task<IncomeExpenseReportDto> GetIncomeExpenseAsync(ReportQuery query, CancellationToken ct = default)
    {
        // Gelir-gider raporu doğası gereği aylıktır; istek gruplaması yok sayılır.
        var monthly = query with { GroupBy = ReportGroupBy.Month };
        var (from, to, startUtc, endUtc) = Resolve(monthly);

        var incomeSeries = await FilterPayments(monthly, startUtc, endUtc)
            .GroupBy(p => p.ReceivedAtUtc.AddHours(TrHours).Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        var expenseQuery = FilterExpenses(monthly, from, to);
        var expenseSeries = await expenseQuery
            .GroupBy(e => e.ExpenseDate)
            .Select(g => new { Day = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(ct);

        var categoryRows = await expenseQuery
            .GroupBy(e => new { e.CategoryId, e.Category!.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .ToListAsync(ct);
        var byCategory = categoryRows
            .OrderByDescending(c => c.Total)
            .Select(c => new ExpenseCategoryTotalDto(c.CategoryId, c.Name, c.Total, c.Count))
            .ToList();

        var incomeBuckets = Fold(incomeSeries, x => x.Day, ReportGroupBy.Month);
        var expenseBuckets = Fold(expenseSeries, x => x.Day.ToDateTime(TimeOnly.MinValue), ReportGroupBy.Month);

        var series = EnumeratePeriods(from, to, ReportGroupBy.Month)
            .Select(period =>
            {
                var income = incomeBuckets.TryGetValue(period, out var i) ? i.Sum(x => x.Total) : 0m;
                var expense = expenseBuckets.TryGetValue(period, out var e) ? e.Sum(x => x.Total) : 0m;
                return new IncomeExpensePointDto(period, Label(period, ReportGroupBy.Month), income, expense, income - expense);
            })
            .ToList();

        var totalIncome = incomeSeries.Sum(x => x.Total);
        var totalExpense = expenseSeries.Sum(x => x.Total);

        return new IncomeExpenseReportDto(
            new ReportPeriodDto(from, to, ReportGroupBy.Month),
            series, byCategory, totalIncome, totalExpense, totalIncome - totalExpense);
    }

    // ---- Hekim performansı ----

    public async Task<DoctorPerformanceReportDto> GetDoctorPerformanceAsync(
        ReportQuery query, CancellationToken ct = default)
    {
        var (from, to, startUtc, endUtc) = Resolve(query);

        // (1) Hekim başına üretim: adet, ciro, tekil hasta.
        var production = await FilterTreatments(query, startUtc, endUtc)
            .GroupBy(t => new { t.DoctorUserId, t.PatientId })
            .Select(g => new
            {
                g.Key.DoctorUserId,
                g.Key.PatientId,
                Revenue = g.Sum(t => t.Price - t.DiscountAmount),
                Count = g.Count(),
            })
            .ToListAsync(ct);

        // (2) Hasta bazında dönem tahsilatı — hekime üretim payı oranında dağıtılır.
        var collections = await FilterPayments(query with { DoctorId = null }, startUtc, endUtc)
            .Where(p => p.PatientId != null)
            .GroupBy(p => p.PatientId!.Value)
            .Select(g => new { PatientId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.PatientId, x => x.Total, ct);

        // (3) Randevu sayıları ve gelmeme.
        var appointments = await FilterAppointments(query, startUtc, endUtc)
            .GroupBy(a => new { a.DoctorUserId, a.Status })
            .Select(g => new { g.Key.DoctorUserId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        // (4) Hekim künyeleri (AppUser global filtreye tabi değil; TenantId elle süzülür).
        var tenantId = db.CurrentTenantId;
        var doctorIds = production.Select(p => p.DoctorUserId)
            .Concat(appointments.Select(a => a.DoctorUserId))
            .Distinct()
            .ToList();
        var doctors = await db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && (doctorIds.Contains(u.Id) || u.UserType == UserType.Dentist))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Branch })
            .ToListAsync(ct);

        // Hastanın dönem üretimi (tüm hekimler) — tahsilat payı bu paydadan bulunur.
        var patientProduction = production
            .GroupBy(p => p.PatientId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Revenue));

        var rows = doctors
            .Where(d => query.DoctorId is not { } doctorId || d.Id == doctorId)
            .Select(d =>
            {
                var own = production.Where(p => p.DoctorUserId == d.Id).ToList();
                var appts = appointments.Where(a => a.DoctorUserId == d.Id).ToList();
                var total = appts.Sum(a => a.Count);
                var cancelled = appts.Where(a => a.Status == AppointmentStatus.Cancelled).Sum(a => a.Count);
                var noShow = appts.Where(a => a.Status == AppointmentStatus.NoShow).Sum(a => a.Count);
                var denominator = total - cancelled;

                var collected = own.Sum(p =>
                {
                    if (!collections.TryGetValue(p.PatientId, out var paid)) return 0m;
                    var patientTotal = patientProduction.GetValueOrDefault(p.PatientId);
                    return patientTotal <= 0m ? 0m : Math.Round(paid * (p.Revenue / patientTotal), 2);
                });

                return new DoctorPerformanceRowDto(
                    d.Id, $"{d.FirstName} {d.LastName}", d.Branch,
                    PatientCount: own.Select(p => p.PatientId).Distinct().Count(),
                    TreatmentCount: own.Sum(p => p.Count),
                    ProducedRevenue: own.Sum(p => p.Revenue),
                    CollectedRevenue: collected,
                    AppointmentCount: total,
                    NoShowCount: noShow,
                    NoShowRate: Rate(noShow, denominator));
            })
            .OrderByDescending(r => r.ProducedRevenue)
            .ThenBy(r => r.DoctorName)
            .ToList();

        return new DoctorPerformanceReportDto(new ReportPeriodDto(from, to, query.GroupBy), rows);
    }

    // ---- Tahsilat + yaşlandırma ----

    public async Task<CollectionsReportDto> GetCollectionsAsync(ReportQuery query, CancellationToken ct = default)
    {
        var (from, to, startUtc, endUtc) = Resolve(query);
        var payments = FilterPayments(query, startUtc, endUtc);

        var daily = await payments
            .GroupBy(p => p.ReceivedAtUtc.AddHours(TrHours).Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var byMethod = await LoadMethodTotalsAsync(payments, ct);

        var buckets = Fold(daily, x => x.Day, query.GroupBy);
        var series = EnumeratePeriods(from, to, query.GroupBy)
            .Select(period => new CollectionPointDto(
                period, Label(period, query.GroupBy),
                buckets.TryGetValue(period, out var b) ? b.Sum(x => x.Total) : 0m,
                buckets.TryGetValue(period, out var c) ? c.Sum(x => x.Count) : 0))
            .ToList();

        var (aging, outstanding) = await BuildAgingAsync(query.ClinicId, ct);

        return new CollectionsReportDto(
            new ReportPeriodDto(from, to, query.GroupBy),
            series, byMethod,
            daily.Sum(x => x.Total), daily.Sum(x => x.Count),
            aging, outstanding);
    }

    /// <summary>
    /// Yaşlandırma: borcu olan HASTA carilerinin açık bakiyesi, alacaklar en eski borçtan
    /// başlayarak (FIFO) mahsup edilerek kovalanır. Kurum carileri bilinçli olarak kapsam dışıdır
    /// (kurum tahsilatı sözleşme dönemine göre işler, gün yaşlandırması yanıltıcı olur).
    /// </summary>
    private async Task<(IReadOnlyList<AgingBucketDto> Buckets, decimal Outstanding)> BuildAgingAsync(
        long? clinicId, CancellationToken ct)
    {
        var debtors = db.Patients.AsNoTracking().Where(p => p.Balance > 0m);
        if (clinicId is { } clinic) debtors = debtors.Where(p => p.ClinicId == clinic);

        var outstanding = await debtors.SumAsync(p => p.Balance, ct);

        // Tek sorgu: borçlu hastaların TÜM cari hareketleri, tarih sırasıyla.
        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.PatientId != null && debtors.Any(p => p.Id == e.PatientId))
            .OrderBy(e => e.PatientId).ThenBy(e => e.EntryDate).ThenBy(e => e.Id)
            .Select(e => new { PatientId = e.PatientId!.Value, e.EntryDate, e.Debit, e.Credit })
            .ToListAsync(ct);

        var today = TrTime.ToLocalDate(DateTime.UtcNow);
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["0-30"] = 0m, ["31-60"] = 0m, ["61-90"] = 0m, ["90+"] = 0m,
        };
        var counts = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal)
        {
            ["0-30"] = [], ["31-60"] = [], ["61-90"] = [], ["90+"] = [],
        };

        foreach (var group in entries.GroupBy(e => e.PatientId))
        {
            // Açık borç kalemleri (tarih, kalan tutar) — alacaklar en eskiden mahsup edilir.
            var open = new List<(DateOnly Date, decimal Amount)>();
            var credit = 0m;
            foreach (var entry in group)
            {
                credit += entry.Credit;
                if (entry.Debit > 0m) open.Add((entry.EntryDate, entry.Debit));
            }

            for (var i = 0; i < open.Count && credit > 0m; i++)
            {
                var applied = Math.Min(open[i].Amount, credit);
                open[i] = (open[i].Date, open[i].Amount - applied);
                credit -= applied;
            }

            foreach (var (date, amount) in open.Where(o => o.Amount > 0m))
            {
                var age = today.DayNumber - date.DayNumber;
                var bucket = age switch
                {
                    <= 30 => "0-30",
                    <= 60 => "31-60",
                    <= 90 => "61-90",
                    _ => "90+",
                };
                totals[bucket] += amount;
                counts[bucket].Add(group.Key);
            }
        }

        var buckets = new[] { "0-30", "31-60", "61-90", "90+" }
            .Select(b => new AgingBucketDto(b, decimal.Round(totals[b], 2), counts[b].Count))
            .ToList();
        return (buckets, outstanding);
    }

    // ---- Tedavi dökümü ----

    public async Task<TreatmentsReportDto> GetTreatmentsAsync(ReportQuery query, CancellationToken ct = default)
    {
        var (from, to, startUtc, endUtc) = Resolve(query);

        var source = FilterTreatments(query, startUtc, endUtc);
        if (query.CategoryId is { } categoryId)
            source = source.Where(t => t.TreatmentDefinition!.CategoryId == categoryId);

        var grouped = await source
            .GroupBy(t => new
            {
                t.TreatmentDefinitionId,
                t.TreatmentDefinition!.Code,
                t.TreatmentDefinition.Name,
                t.TreatmentDefinition.CategoryId,
                CategoryName = t.TreatmentDefinition.Category!.Name,
            })
            .Select(g => new
            {
                g.Key,
                Count = g.Count(),
                Gross = g.Sum(t => t.Price),
                Discount = g.Sum(t => t.DiscountAmount),
                Net = g.Sum(t => t.Price - t.DiscountAmount),
            })
            .ToListAsync(ct);

        var rows = grouped
            .OrderByDescending(g => g.Net)
            .Select(g => new TreatmentReportRowDto(
                g.Key.TreatmentDefinitionId, g.Key.Code, g.Key.Name, g.Key.CategoryId, g.Key.CategoryName,
                g.Count, g.Gross, g.Discount, g.Net))
            .ToList();

        var byCategory = rows
            .GroupBy(r => new { r.CategoryId, r.CategoryName })
            .Select(g => new TreatmentCategoryTotalDto(
                g.Key.CategoryId, g.Key.CategoryName, g.Sum(r => r.Count), g.Sum(r => r.NetAmount)))
            .OrderByDescending(c => c.NetAmount)
            .ToList();

        return new TreatmentsReportDto(
            new ReportPeriodDto(from, to, query.GroupBy),
            rows, byCategory, rows.Sum(r => r.Count), rows.Sum(r => r.NetAmount));
    }

    // ---- Randevu ----

    public async Task<AppointmentsReportDto> GetAppointmentsAsync(ReportQuery query, CancellationToken ct = default)
    {
        var (from, to, startUtc, endUtc) = Resolve(query);
        var source = FilterAppointments(query, startUtc, endUtc);

        var statusRows = await source
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byStatus = statusRows
            .OrderBy(s => s.Status)
            .Select(s => new AppointmentStatusTotalDto(s.Status, s.Count))
            .ToList();

        var daily = await source
            .GroupBy(a => new { Day = a.StartUtc.AddHours(TrHours).Date, a.Status })
            .Select(g => new
            {
                g.Key.Day,
                g.Key.Status,
                Count = g.Count(),
                Minutes = g.Sum(a => EF.Functions.DateDiffMinute(a.StartUtc, a.EndUtc)),
            })
            .ToListAsync(ct);

        // Kapasite: hekim çalışma saatleri (haftanın gününe göre). Tek sorgu, günlere bellekte yayılır.
        var workingHours = await db.DoctorWorkingHours.AsNoTracking()
            .Where(w => query.DoctorId == null || w.DoctorUserId == query.DoctorId)
            .Where(w => query.ClinicId == null || w.ClinicId == query.ClinicId)
            .Select(w => new { w.DayOfWeek, w.StartTime, w.EndTime })
            .ToListAsync(ct);
        var capacityByDayOfWeek = workingHours
            .GroupBy(w => w.DayOfWeek)
            .ToDictionary(g => g.Key, g => (int)g.Sum(w => (w.EndTime - w.StartTime).TotalMinutes));

        var trend = EnumeratePeriods(from, to, query.GroupBy)
            .Select(period =>
            {
                var days = DaysOfPeriod(period, query.GroupBy, from, to).ToList();
                var slice = daily.Where(d => days.Contains(DateOnly.FromDateTime(d.Day))).ToList();

                var total = slice.Sum(d => d.Count);
                var completed = slice.Where(d => d.Status == AppointmentStatus.Completed).Sum(d => d.Count);
                var noShow = slice.Where(d => d.Status == AppointmentStatus.NoShow).Sum(d => d.Count);
                var cancelled = slice.Where(d => d.Status == AppointmentStatus.Cancelled).Sum(d => d.Count);
                var booked = slice.Where(d => d.Status != AppointmentStatus.Cancelled).Sum(d => d.Minutes);
                var capacity = days.Sum(d => capacityByDayOfWeek.GetValueOrDefault(d.DayOfWeek));

                return new AppointmentTrendPointDto(
                    period, Label(period, query.GroupBy), total, completed, noShow, cancelled,
                    Rate(noShow, total - cancelled), booked, capacity, Rate(booked, capacity));
            })
            .ToList();

        var totalCount = byStatus.Sum(s => s.Count);
        var totalNoShow = byStatus.Where(s => s.Status == AppointmentStatus.NoShow).Sum(s => s.Count);
        var totalCancelled = byStatus.Where(s => s.Status == AppointmentStatus.Cancelled).Sum(s => s.Count);
        var totalBooked = trend.Sum(t => t.BookedMinutes);
        var totalCapacity = trend.Sum(t => t.CapacityMinutes);

        return new AppointmentsReportDto(
            new ReportPeriodDto(from, to, query.GroupBy),
            byStatus, trend, totalCount, totalNoShow, totalCancelled,
            Rate(totalNoShow, totalCount - totalCancelled), Rate(totalBooked, totalCapacity));
    }

    // ---- Borçlu hastalar ----

    public async Task<PagedResult<DebtorRowDto>> GetDebtorsAsync(DebtorQuery query, CancellationToken ct = default)
    {
        var minBalance = query.MinBalance <= 0m ? 0.01m : query.MinBalance;
        var source = db.Patients.AsNoTracking().Where(p => p.Balance >= minBalance);

        var page = new PageRequest(query.Page, query.PageSize);
        var totalCount = await source.CountAsync(ct);

        // İlişkili son işlem/randevu tarihi korelasyonlu alt sorgudur — satır başına ek gidiş yok.
        var items = await source
            .OrderByDescending(p => p.Balance).ThenBy(p => p.Id)
            .Skip(page.Skip).Take(page.EffectivePageSize)
            .Select(p => new DebtorRowDto(
                p.Id, p.FileNo, p.FirstName + " " + p.LastName, p.Phone, p.Balance,
                db.LedgerEntries.Where(e => e.PatientId == p.Id)
                    .OrderByDescending(e => e.EntryDate).Select(e => (DateOnly?)e.EntryDate).FirstOrDefault(),
                db.Appointments.Where(a => a.PatientId == p.Id)
                    .OrderByDescending(a => a.StartUtc).Select(a => (DateTime?)a.StartUtc).FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedResult<DebtorRowDto>(items, page.Page, page.EffectivePageSize, totalCount);
    }

    // ---- Excel dışa aktarım ----

    public async Task<ReportFileDto> ExportAsync(
        string reportKey, ReportQuery query, DebtorQuery debtorQuery, CancellationToken ct = default)
    {
        var sheets = reportKey switch
        {
            ReportKeys.Revenue => RevenueSheets(await GetRevenueAsync(query, ct)),
            ReportKeys.IncomeExpense => IncomeExpenseSheets(await GetIncomeExpenseAsync(query, ct)),
            ReportKeys.DoctorPerformance => DoctorPerformanceSheets(await GetDoctorPerformanceAsync(query, ct)),
            ReportKeys.Collections => CollectionsSheets(await GetCollectionsAsync(query, ct)),
            ReportKeys.Treatments => TreatmentsSheets(await GetTreatmentsAsync(query, ct)),
            ReportKeys.Appointments => AppointmentsSheets(await GetAppointmentsAsync(query, ct)),
            ReportKeys.Debtors => DebtorsSheets(await GetDebtorsAsync(
                debtorQuery with { PageSize = PageRequest.MaxPageSize }, ct)),
            _ => throw new KeyNotFoundException($"Bilinmeyen rapor: '{reportKey}'."),
        };

        var stamp = DateTime.UtcNow.Add(TrTime.Offset).ToString("yyyyMMdd-HHmm");
        return new ReportFileDto(
            ExcelExporter.Build(sheets),
            $"{reportKey}-{stamp}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static IReadOnlyList<ExcelSheet> RevenueSheets(RevenueReportDto report) =>
    [
        new("Ciro",
            [
                new("Dönem", ExcelValueKind.Text, 14),
                new("Tedavi Cirosu", ExcelValueKind.Money, 16),
                new("Tahsilat", ExcelValueKind.Money, 16),
                new("Tedavi Adedi", ExcelValueKind.Integer, 14),
            ],
            [.. report.Series.Select(p => new object?[] { p.PeriodLabel, p.TreatmentRevenue, p.Collected, p.TreatmentCount }),
                new object?[] { "TOPLAM", report.TotalTreatmentRevenue, report.TotalCollected, report.TotalTreatmentCount }]),
        new("Yöntem Kırılımı",
            [new("Yöntem", ExcelValueKind.Text, 18), new("Tutar", ExcelValueKind.Money, 16), new("Adet", ExcelValueKind.Integer, 10)],
            [.. report.ByMethod.Select(m => new object?[] { ReportLabels.Method(m.Method), m.Total, m.Count })]),
    ];

    private static IReadOnlyList<ExcelSheet> IncomeExpenseSheets(IncomeExpenseReportDto report) =>
    [
        new("Gelir-Gider",
            [
                new("Ay", ExcelValueKind.Text, 14),
                new("Gelir", ExcelValueKind.Money, 16),
                new("Gider", ExcelValueKind.Money, 16),
                new("Net", ExcelValueKind.Money, 16),
            ],
            [.. report.Series.Select(p => new object?[] { p.PeriodLabel, p.Income, p.Expense, p.Net }),
                new object?[] { "TOPLAM", report.TotalIncome, report.TotalExpense, report.NetProfit }]),
        new("Gider Kategorileri",
            [new("Kategori", ExcelValueKind.Text, 24), new("Tutar", ExcelValueKind.Money, 16), new("Adet", ExcelValueKind.Integer, 10)],
            [.. report.ExpensesByCategory.Select(c => new object?[] { c.CategoryName, c.Total, c.Count })]),
    ];

    private static IReadOnlyList<ExcelSheet> DoctorPerformanceSheets(DoctorPerformanceReportDto report) =>
    [
        new("Hekim Performansı",
            [
                new("Hekim", ExcelValueKind.Text, 24),
                new("Branş", ExcelValueKind.Text, 20),
                new("Hasta", ExcelValueKind.Integer, 10),
                new("Tedavi", ExcelValueKind.Integer, 10),
                new("Üretilen Ciro", ExcelValueKind.Money, 16),
                new("Tahsil Edilen", ExcelValueKind.Money, 16),
                new("Randevu", ExcelValueKind.Integer, 10),
                new("Gelmeyen", ExcelValueKind.Integer, 10),
                new("Gelmeme Oranı", ExcelValueKind.Percent, 14),
            ],
            [.. report.Rows.Select(r => new object?[]
            {
                r.DoctorName, r.Branch, r.PatientCount, r.TreatmentCount, r.ProducedRevenue,
                r.CollectedRevenue, r.AppointmentCount, r.NoShowCount, r.NoShowRate,
            })]),
    ];

    private static IReadOnlyList<ExcelSheet> CollectionsSheets(CollectionsReportDto report) =>
    [
        new("Tahsilat",
            [new("Dönem", ExcelValueKind.Text, 14), new("Tutar", ExcelValueKind.Money, 16), new("Adet", ExcelValueKind.Integer, 10)],
            [.. report.Series.Select(p => new object?[] { p.PeriodLabel, p.Total, p.Count }),
                new object?[] { "TOPLAM", report.TotalCollected, report.TotalCount }]),
        new("Yöntem Kırılımı",
            [new("Yöntem", ExcelValueKind.Text, 18), new("Tutar", ExcelValueKind.Money, 16), new("Adet", ExcelValueKind.Integer, 10)],
            [.. report.ByMethod.Select(m => new object?[] { ReportLabels.Method(m.Method), m.Total, m.Count })]),
        new("Yaşlandırma",
            [new("Kova (gün)", ExcelValueKind.Text, 14), new("Açık Bakiye", ExcelValueKind.Money, 16), new("Hasta", ExcelValueKind.Integer, 10)],
            [.. report.Aging.Select(a => new object?[] { a.Bucket, a.Amount, a.PatientCount }),
                new object?[] { "TOPLAM", report.TotalOutstanding, null }]),
    ];

    private static IReadOnlyList<ExcelSheet> TreatmentsSheets(TreatmentsReportDto report) =>
    [
        new("Tedaviler",
            [
                new("Kod", ExcelValueKind.Text, 12),
                new("Tedavi", ExcelValueKind.Text, 36),
                new("Kategori", ExcelValueKind.Text, 22),
                new("Adet", ExcelValueKind.Integer, 10),
                new("Brüt", ExcelValueKind.Money, 14),
                new("İndirim", ExcelValueKind.Money, 14),
                new("Net", ExcelValueKind.Money, 14),
            ],
            [.. report.Rows.Select(r => new object?[]
                { r.Code, r.Name, r.CategoryName, r.Count, r.GrossAmount, r.DiscountAmount, r.NetAmount }),
                new object?[] { "TOPLAM", null, null, report.TotalCount, null, null, report.TotalNetAmount }]),
        new("Kategori Kırılımı",
            [new("Kategori", ExcelValueKind.Text, 24), new("Adet", ExcelValueKind.Integer, 10), new("Net", ExcelValueKind.Money, 16)],
            [.. report.ByCategory.Select(c => new object?[] { c.CategoryName, c.Count, c.NetAmount })]),
    ];

    private static IReadOnlyList<ExcelSheet> AppointmentsSheets(AppointmentsReportDto report) =>
    [
        new("Randevu Trendi",
            [
                new("Dönem", ExcelValueKind.Text, 14),
                new("Toplam", ExcelValueKind.Integer, 10),
                new("Tamamlanan", ExcelValueKind.Integer, 12),
                new("Gelmeyen", ExcelValueKind.Integer, 10),
                new("İptal", ExcelValueKind.Integer, 10),
                new("Gelmeme Oranı", ExcelValueKind.Percent, 14),
                new("Dolu Dakika", ExcelValueKind.Integer, 12),
                new("Kapasite Dakika", ExcelValueKind.Integer, 14),
                new("Doluluk", ExcelValueKind.Percent, 12),
            ],
            [.. report.Trend.Select(t => new object?[]
            {
                t.PeriodLabel, t.Total, t.Completed, t.NoShow, t.Cancelled, t.NoShowRate,
                t.BookedMinutes, t.CapacityMinutes, t.OccupancyRate,
            })]),
        new("Durum Dağılımı",
            [new("Durum", ExcelValueKind.Text, 18), new("Adet", ExcelValueKind.Integer, 10)],
            [.. report.ByStatus.Select(s => new object?[] { ReportLabels.AppointmentStatus(s.Status), s.Count })]),
    ];

    private static IReadOnlyList<ExcelSheet> DebtorsSheets(PagedResult<DebtorRowDto> page) =>
    [
        new("Borçlu Hastalar",
            [
                new("Dosya No", ExcelValueKind.Text, 12),
                new("Hasta", ExcelValueKind.Text, 28),
                new("Telefon", ExcelValueKind.Text, 18),
                new("Bakiye", ExcelValueKind.Money, 16),
                new("Son İşlem", ExcelValueKind.Date, 14),
                new("Son Randevu", ExcelValueKind.DateTime, 18),
            ],
            [.. page.Items.Select(d => new object?[]
            {
                d.FileNo, d.FullName, d.Phone, d.Balance, d.LastEntryDate,
                d.LastAppointmentUtc?.Add(TrTime.Offset),
            })]),
    ];

    // ---- Ortak filtreler ----

    /// <summary>
    /// Yöntem kırılımı. Gruplama sonucunu DOĞRUDAN pozisyonel record'a projekte etmek EF Core'da
    /// çevrilemiyor; anonim tipe toplanıp bellekte eşlenir (yine tek sorgu).
    /// </summary>
    private static async Task<IReadOnlyList<PaymentMethodTotalDto>> LoadMethodTotalsAsync(
        IQueryable<Payment> payments, CancellationToken ct)
    {
        var rows = await payments
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Total = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync(ct);
        return [.. rows.OrderBy(r => r.Method).Select(r => new PaymentMethodTotalDto(r.Method, r.Total, r.Count))];
    }

    private IQueryable<TreatmentRecord> FilterTreatments(ReportQuery query, DateTime startUtc, DateTime endUtc)
    {
        var source = db.TreatmentRecords.AsNoTracking()
            .Where(t => t.Status == TreatmentRecordStatus.Done
                        && t.PerformedAtUtc != null
                        && t.PerformedAtUtc >= startUtc && t.PerformedAtUtc < endUtc);
        if (query.DoctorId is { } doctorId) source = source.Where(t => t.DoctorUserId == doctorId);
        if (query.ClinicId is { } clinicId) source = source.Where(t => t.ClinicId == clinicId);
        return source;
    }

    /// <summary>
    /// Tahsilatlar. Hekim süzgeci verildiğinde tahsilat, dönemde o hekimden tedavi görmüş
    /// hastalarla sınırlanır (tahsilatın kendisi hekime bağlı değildir).
    /// </summary>
    private IQueryable<Payment> FilterPayments(ReportQuery query, DateTime startUtc, DateTime endUtc)
    {
        var source = db.Payments.AsNoTracking()
            .Where(p => p.ReceivedAtUtc >= startUtc && p.ReceivedAtUtc < endUtc);
        if (query.ClinicId is { } clinicId) source = source.Where(p => p.ClinicId == clinicId);
        if (query.DoctorId is { } doctorId)
        {
            source = source.Where(p => p.PatientId != null && db.TreatmentRecords.Any(t =>
                t.PatientId == p.PatientId && t.DoctorUserId == doctorId
                && t.Status == TreatmentRecordStatus.Done
                && t.PerformedAtUtc >= startUtc && t.PerformedAtUtc < endUtc));
        }
        return source;
    }

    private IQueryable<Expense> FilterExpenses(ReportQuery query, DateOnly from, DateOnly to)
    {
        var source = db.Expenses.AsNoTracking().Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to);
        if (query.ClinicId is { } clinicId) source = source.Where(e => e.ClinicId == clinicId);
        return source;
    }

    private IQueryable<Appointment> FilterAppointments(ReportQuery query, DateTime startUtc, DateTime endUtc)
    {
        var source = db.Appointments.AsNoTracking()
            .Where(a => a.StartUtc >= startUtc && a.StartUtc < endUtc);
        if (query.DoctorId is { } doctorId) source = source.Where(a => a.DoctorUserId == doctorId);
        if (query.ClinicId is { } clinicId) source = source.Where(a => a.ClinicId == clinicId);
        return source;
    }

    // ---- Dönem yardımcıları ----

    private static (DateOnly From, DateOnly To, DateTime StartUtc, DateTime EndUtc) Resolve(ReportQuery query)
    {
        var today = TrTime.ToLocalDate(DateTime.UtcNow);
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-DefaultRangeDays);
        if (from > to) (from, to) = (to, from);
        return (from, to, TrTime.DayRangeUtc(from).StartUtc, TrTime.DayRangeUtc(to).EndUtc);
    }

    /// <summary>Günlük SQL kovalarını istenen dönem kovasına (gün/hafta/ay) katlar.</summary>
    private static Dictionary<DateOnly, List<T>> Fold<T>(
        IEnumerable<T> source, Func<T, DateTime> daySelector, ReportGroupBy groupBy) =>
        source
            .GroupBy(x => PeriodStart(DateOnly.FromDateTime(daySelector(x)), groupBy))
            .ToDictionary(g => g.Key, g => g.ToList());

    private static DateOnly PeriodStart(DateOnly date, ReportGroupBy groupBy) => groupBy switch
    {
        // Hafta pazartesi başlar (TR takvim alışkanlığı).
        ReportGroupBy.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        ReportGroupBy.Month => new DateOnly(date.Year, date.Month, 1),
        _ => date,
    };

    private static IEnumerable<DateOnly> EnumeratePeriods(DateOnly from, DateOnly to, ReportGroupBy groupBy)
    {
        var cursor = PeriodStart(from, groupBy);
        var last = PeriodStart(to, groupBy);
        while (cursor <= last)
        {
            yield return cursor;
            cursor = groupBy switch
            {
                ReportGroupBy.Week => cursor.AddDays(7),
                ReportGroupBy.Month => cursor.AddMonths(1),
                _ => cursor.AddDays(1),
            };
        }
    }

    private static IEnumerable<DateOnly> DaysOfPeriod(
        DateOnly period, ReportGroupBy groupBy, DateOnly from, DateOnly to)
    {
        var end = groupBy switch
        {
            ReportGroupBy.Week => period.AddDays(6),
            ReportGroupBy.Month => period.AddMonths(1).AddDays(-1),
            _ => period,
        };
        for (var d = period < from ? from : period; d <= (end > to ? to : end); d = d.AddDays(1))
            yield return d;
    }

    private static string Label(DateOnly period, ReportGroupBy groupBy) => groupBy switch
    {
        ReportGroupBy.Month => period.ToString("MM.yyyy"),
        _ => period.ToString("dd.MM.yyyy"),
    };

    /// <summary>Yüzde (0-100) döner; payda sıfırsa 0.</summary>
    private static decimal Rate(decimal numerator, decimal denominator) =>
        denominator <= 0m ? 0m : decimal.Round(numerator * 100m / denominator, 2);
}
