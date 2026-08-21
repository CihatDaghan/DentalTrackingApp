using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Finance;
using Dental.Domain.Common;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// I aşaması "bitti" kriterleri (raporlar): bilinen seed verisiyle beklenen toplamlar,
/// yaşlandırma kovaları, Excel dışa aktarım, gösterge paneli alanları, kiracı izolasyonu
/// ve hasta kartı "Rapor" sekmesi (tedavi dökümü / durum raporu / proforma).
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class ReportTests(ApiFixture fx)
{
    private const string Extraction = "DIS-080";

    /// <summary>
    /// Kesin toplam doğrulaması için izole kiracıya bilinen veri kurar:
    /// A: 1000-100 = 900 borç, 400 nakit tahsilat → bakiye 500
    /// B: 2000 borç, 500 kart tahsilatı → bakiye 1500
    /// C: 100 gün önce tarihli 1000 açılış borcu (yaşlandırma 90+ kovası)
    /// gider: 300
    /// </summary>
    private async Task<(TestTenant Tenant, string Token, long PatientA, long PatientB, long PatientC)> SeedAsync()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Rapor");
        var token = await TestApi.LoginAsync(fx, tenant);
        var definitionId = await TestApi.FindDefinitionAsync(fx, token, Extraction);

        var patientA = await TestApi.CreatePatientAsync(fx, token, "Ali", "Alacak");
        var patientB = await TestApi.CreatePatientAsync(fx, token, "Berk", "Borçlu");
        var patientC = await TestApi.CreatePatientAsync(fx, token, "Cem", "Eski");

        await TestApi.AddTreatmentAsync(fx, token, patientA, definitionId, 1000m, 100m);
        await TestApi.AddTreatmentAsync(fx, token, patientB, definitionId, 2000m, 0m, toothNumber: "46");
        await TestApi.PayAsync(fx, token, patientA, 400m, PaymentMethod.Cash);
        await TestApi.PayAsync(fx, token, patientB, 500m, PaymentMethod.CreditCardPos);

        // 100 gün geriye tarihli açılış borcu — yaşlandırmanın 90+ kovasını besler.
        var scopeFactory = fx.Services.GetRequiredService<ITenantScopeFactory>();
        using (var scope = scopeFactory.CreateScope(tenant.TenantId, tenant.ClinicId, tenant.OwnerUserId))
        {
            var ledger = scope.ServiceProvider.GetRequiredService<ILedgerService>();
            await ledger.AddEntryAsync(new LedgerEntryCreateRequest(
                LedgerAccountType.Patient, patientC, null, LedgerEntryType.OpeningBalance,
                Debit: 1000m, Credit: 0m, "Eski devir",
                EntryDate: TrTime.ToLocalDate(DateTime.UtcNow).AddDays(-100),
                ClinicId: tenant.ClinicId));
        }

        var categoryId = (await TestApi.GetJsonAsync(fx, "/api/v1/expense-categories", token))
            .EnumerateArray().First().GetProperty("id").GetInt64();
        var expense = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/expenses", token, new
        {
            categoryId,
            amount = 300m,
            expenseDate = TrTime.ToLocalDate(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            method = (byte)PaymentMethod.Cash,
        }));
        Assert.Equal(HttpStatusCode.Created, expense.StatusCode);

        return (tenant, token, patientA, patientB, patientC);
    }

    [Fact]
    public async Task Revenue_MatchesKnownSeedTotals()
    {
        var (_, token, _, _, _) = await SeedAsync();

        var report = await TestApi.GetJsonAsync(fx, "/api/v1/reports/revenue?groupBy=month", token);

        // 900 + 2000 = 2900 üretim; 400 + 500 = 900 tahsilat.
        Assert.Equal(2900m, report.GetProperty("totalTreatmentRevenue").GetDecimal());
        Assert.Equal(900m, report.GetProperty("totalCollected").GetDecimal());
        Assert.Equal(2, report.GetProperty("totalTreatmentCount").GetInt32());

        var byMethod = report.GetProperty("byMethod").EnumerateArray().ToList();
        Assert.Equal(2, byMethod.Count);
        Assert.Equal(400m, byMethod.Single(m => m.GetProperty("method").GetInt32() == (int)PaymentMethod.Cash)
            .GetProperty("total").GetDecimal());
        Assert.Equal(500m, byMethod.Single(m => m.GetProperty("method").GetInt32() == (int)PaymentMethod.CreditCardPos)
            .GetProperty("total").GetDecimal());

        // Seri gün gün değil ay ay kovalanır; bugünün ayı toplamı taşır.
        var today = TrTime.ToLocalDate(DateTime.UtcNow);
        var currentMonth = report.GetProperty("series").EnumerateArray()
            .Single(p => DateOnly.Parse(p.GetProperty("period").GetString()!) == new DateOnly(today.Year, today.Month, 1));
        Assert.Equal(2900m, currentMonth.GetProperty("treatmentRevenue").GetDecimal());
        Assert.Equal(900m, currentMonth.GetProperty("collected").GetDecimal());
    }

    [Fact]
    public async Task IncomeExpense_NetProfitIsIncomeMinusExpense()
    {
        var (_, token, _, _, _) = await SeedAsync();

        var report = await TestApi.GetJsonAsync(fx, "/api/v1/reports/income-expense", token);

        Assert.Equal(900m, report.GetProperty("totalIncome").GetDecimal());
        Assert.Equal(300m, report.GetProperty("totalExpense").GetDecimal());
        Assert.Equal(600m, report.GetProperty("netProfit").GetDecimal());
        var category = Assert.Single(report.GetProperty("expensesByCategory").EnumerateArray());
        Assert.Equal(300m, category.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Collections_AgingBucketsSplitByDebtAge()
    {
        var (_, token, _, _, _) = await SeedAsync();

        var report = await TestApi.GetJsonAsync(fx, "/api/v1/reports/collections", token);

        Assert.Equal(900m, report.GetProperty("totalCollected").GetDecimal());
        // Açık bakiye: 500 (A) + 1500 (B) + 1000 (C) = 3000.
        Assert.Equal(3000m, report.GetProperty("totalOutstanding").GetDecimal());

        var aging = report.GetProperty("aging").EnumerateArray()
            .ToDictionary(b => b.GetProperty("bucket").GetString()!, b => b);
        Assert.Equal(2000m, aging["0-30"].GetProperty("amount").GetDecimal());
        Assert.Equal(2, aging["0-30"].GetProperty("patientCount").GetInt32());
        Assert.Equal(0m, aging["31-60"].GetProperty("amount").GetDecimal());
        Assert.Equal(0m, aging["61-90"].GetProperty("amount").GetDecimal());
        Assert.Equal(1000m, aging["90+"].GetProperty("amount").GetDecimal());
        Assert.Equal(1, aging["90+"].GetProperty("patientCount").GetInt32());

        // Kovaların toplamı açık bakiyeye eşit olmalı (FIFO mahsup tutarlılığı).
        var bucketSum = aging.Values.Sum(b => b.GetProperty("amount").GetDecimal());
        Assert.Equal(report.GetProperty("totalOutstanding").GetDecimal(), bucketSum);
    }

    [Fact]
    public async Task Treatments_And_Debtors_ReturnExpectedRows()
    {
        var (_, token, _, _, _) = await SeedAsync();

        var treatments = await TestApi.GetJsonAsync(fx, "/api/v1/reports/treatments", token);
        var row = Assert.Single(treatments.GetProperty("rows").EnumerateArray());
        Assert.Equal(Extraction, row.GetProperty("code").GetString());
        Assert.Equal(2, row.GetProperty("count").GetInt32());
        Assert.Equal(3000m, row.GetProperty("grossAmount").GetDecimal());
        Assert.Equal(100m, row.GetProperty("discountAmount").GetDecimal());
        Assert.Equal(2900m, row.GetProperty("netAmount").GetDecimal());
        Assert.Equal(2900m, treatments.GetProperty("totalNetAmount").GetDecimal());
        Assert.Single(treatments.GetProperty("byCategory").EnumerateArray());

        var debtors = await TestApi.GetJsonAsync(fx, "/api/v1/reports/debtors?minBalance=1", token);
        Assert.Equal(3, debtors.GetProperty("totalCount").GetInt32());
        // Bakiyeye göre azalan sıralı: B (1500) > C (1000) > A (500).
        var balances = debtors.GetProperty("items").EnumerateArray()
            .Select(d => d.GetProperty("balance").GetDecimal()).ToList();
        Assert.Equal([1500m, 1000m, 500m], balances);
    }

    [Fact]
    public async Task DoctorPerformance_AttributesProductionAndNoShow()
    {
        var (tenant, token, patientA, _, _) = await SeedAsync();

        // Randevu ekle ve birini "gelmedi" yap → gelmeme oranı.
        var start = DateTime.UtcNow.Date.AddDays(1).AddHours(6);
        foreach (var offset in new[] { 0, 2 })
        {
            var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/appointments", token, new
            {
                clinicId = tenant.ClinicId,
                patientId = patientA,
                doctorUserId = tenant.OwnerUserId,
                startUtc = start.AddHours(offset),
                endUtc = start.AddHours(offset).AddMinutes(45),
                type = (byte)AppointmentType.Normal,
            }));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            if (offset != 0) continue;

            var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
            var noShow = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
                $"/api/v1/appointments/{id}/status", token, new { status = (byte)AppointmentStatus.NoShow }));
            Assert.Equal(HttpStatusCode.OK, noShow.StatusCode);
        }

        var to = TrTime.ToLocalDate(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd");
        var report = await TestApi.GetJsonAsync(fx, $"/api/v1/reports/doctor-performance?to={to}", token);
        var owner = report.GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("doctorUserId").GetInt64() == tenant.OwnerUserId);

        Assert.Equal(2, owner.GetProperty("patientCount").GetInt32());
        Assert.Equal(2, owner.GetProperty("treatmentCount").GetInt32());
        Assert.Equal(2900m, owner.GetProperty("producedRevenue").GetDecimal());
        // Tahsilat üretim payına göre dağıtılır; tek hekim olduğundan tamamı ona yazılır.
        Assert.Equal(900m, owner.GetProperty("collectedRevenue").GetDecimal());
        Assert.Equal(2, owner.GetProperty("appointmentCount").GetInt32());
        Assert.Equal(1, owner.GetProperty("noShowCount").GetInt32());
        Assert.Equal(50m, owner.GetProperty("noShowRate").GetDecimal());

        var appointments = await TestApi.GetJsonAsync(fx, $"/api/v1/reports/appointments?to={to}", token);
        Assert.Equal(2, appointments.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, appointments.GetProperty("noShowCount").GetInt32());
        Assert.Equal(50m, appointments.GetProperty("noShowRate").GetDecimal());
    }

    [Fact]
    public async Task Export_ReturnsXlsxForEveryReport()
    {
        var (_, token, _, _, _) = await SeedAsync();

        string[] reports =
            ["revenue", "income-expense", "doctor-performance", "collections", "treatments", "appointments", "debtors"];
        foreach (var report in reports)
        {
            var response = await fx.Client.SendAsync(
                TestApi.Req(HttpMethod.Get, $"/api/v1/reports/{report}/export?format=xlsx", token));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            // XLSX bir ZIP paketidir: dosya "PK" imzasıyla başlar.
            Assert.True(bytes.Length > 1000, $"{report} dosyası boş görünüyor.");
            Assert.Equal("PK", Encoding.ASCII.GetString(bytes, 0, 2));
        }

        var unknown = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Get, "/api/v1/reports/bilinmeyen/export?format=xlsx", token));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task DashboardSummary_FillsEveryCard()
    {
        var (_, token, _, _, _) = await SeedAsync();

        var summary = await TestApi.GetJsonAsync(fx, "/api/v1/dashboard/summary", token);

        Assert.Equal(2900m, summary.GetProperty("todayRevenue").GetDecimal());
        Assert.Equal(2900m, summary.GetProperty("monthRevenue").GetDecimal());
        Assert.Equal(900m, summary.GetProperty("todayCollections").GetDecimal());
        Assert.Equal(300m, summary.GetProperty("todayExpenses").GetDecimal());
        Assert.Equal(3000m, summary.GetProperty("totalOutstanding").GetDecimal());
        Assert.Equal(3, summary.GetProperty("activePatientCount").GetInt32());
        Assert.Equal(30, summary.GetProperty("last30DaysRevenue").GetArrayLength());
        Assert.Equal(2900m, summary.GetProperty("last30DaysRevenue").EnumerateArray()
            .Last().GetProperty("amount").GetDecimal());

        // Bekleyen iş sayaçlarının tamamı yanıtta bulunmalı (ön yüzdeki "—" kartları bunu bekler).
        var pending = summary.GetProperty("pendingWork");
        foreach (var counter in new[]
                 {
                     "overdueLabCases", "lowStockItems", "unsignedConsents",
                     "eInvoiceErrors", "failedMessages", "pendingEnabizPackets",
                 })
        {
            Assert.True(pending.TryGetProperty(counter, out var value), $"{counter} eksik.");
            Assert.True(value.GetInt32() >= 0);
        }
        Assert.True(summary.TryGetProperty("birthdayPatients", out _));
        Assert.True(summary.TryGetProperty("todayAppointmentsByStatus", out _));
    }

    [Fact]
    public async Task DashboardSummary_ListsTodaysBirthdayPatients()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Dogumgunu");
        var token = await TestApi.LoginAsync(fx, tenant);
        var today = TrTime.ToLocalDate(DateTime.UtcNow);
        var birthDate = new DateOnly(1990, today.Month, today.Day);

        await TestApi.CreatePatientAsync(fx, token, "Doğum", "Günü",
            new { birthDate = birthDate.ToString("yyyy-MM-dd") });

        var summary = await TestApi.GetJsonAsync(fx, "/api/v1/dashboard/summary", token);
        var patient = Assert.Single(summary.GetProperty("birthdayPatients").EnumerateArray());
        Assert.Equal("Doğum Günü", patient.GetProperty("fullName").GetString());
        Assert.Equal(today.Year - 1990, patient.GetProperty("age").GetInt32());
    }

    [Fact]
    public async Task Reports_DoNotLeakOtherTenantsData()
    {
        var (_, token, _, _, _) = await SeedAsync();

        // Başka bir kiracıda çok daha büyük tutarlı veri üret.
        var other = await TestApi.CreateTenantAsync(fx, "Yabanci");
        var otherToken = await TestApi.LoginAsync(fx, other);
        var otherDefinition = await TestApi.FindDefinitionAsync(fx, otherToken, Extraction);
        var otherPatient = await TestApi.CreatePatientAsync(fx, otherToken, "Gizli", "Hasta");
        await TestApi.AddTreatmentAsync(fx, otherToken, otherPatient, otherDefinition, 99_000m);
        await TestApi.PayAsync(fx, otherToken, otherPatient, 55_000m, PaymentMethod.BankTransfer);

        // İlk kiracının raporu değişmemeli.
        var revenue = await TestApi.GetJsonAsync(fx, "/api/v1/reports/revenue", token);
        Assert.Equal(2900m, revenue.GetProperty("totalTreatmentRevenue").GetDecimal());
        Assert.Equal(900m, revenue.GetProperty("totalCollected").GetDecimal());
        Assert.DoesNotContain(revenue.GetProperty("byMethod").EnumerateArray(),
            m => m.GetProperty("method").GetInt32() == (int)PaymentMethod.BankTransfer);

        var debtors = await TestApi.GetJsonAsync(fx, "/api/v1/reports/debtors?minBalance=1", token);
        Assert.Equal(3, debtors.GetProperty("totalCount").GetInt32());
        Assert.DoesNotContain(debtors.GetProperty("items").EnumerateArray(),
            d => d.GetProperty("patientId").GetInt64() == otherPatient);

        // Diğer kiracının raporu da yalnız kendi verisini görür.
        var otherRevenue = await TestApi.GetJsonAsync(fx, "/api/v1/reports/revenue", otherToken);
        Assert.Equal(99_000m, otherRevenue.GetProperty("totalTreatmentRevenue").GetDecimal());
    }

    // ---- Hasta kartı "Rapor" sekmesi ----

    [Fact]
    public async Task PatientTreatmentReport_ReturnsRowsAndPdf()
    {
        var (_, token, patientA, _, _) = await SeedAsync();

        var json = await TestApi.GetJsonAsync(fx, $"/api/v1/patients/{patientA}/reports/treatment", token);
        var row = Assert.Single(json.GetProperty("rows").EnumerateArray());
        Assert.Equal(1000m, row.GetProperty("price").GetDecimal());
        Assert.Equal(100m, row.GetProperty("discountAmount").GetDecimal());
        Assert.Equal(900m, row.GetProperty("netAmount").GetDecimal());
        Assert.Equal(900m, json.GetProperty("totalNet").GetDecimal());
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("doctorName").GetString()));

        var pdf = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Get,
            $"/api/v1/patients/{patientA}/reports/treatment?format=pdf", token));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));

        // Üretilen belge hasta dosyasında (MediaFile) görünür.
        var media = await TestApi.GetJsonAsync(fx,
            $"/api/v1/patients/{patientA}/media?category={(int)MediaCategory.PatientReportPdf}", token);
        Assert.Contains(media.EnumerateArray(),
            m => m.GetProperty("fileName").GetString()!.StartsWith("tedavi-dokumu-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PatientStatusReport_ReturnsToothStatusAndPdf()
    {
        var (_, token, patientA, _, _) = await SeedAsync();

        var json = await TestApi.GetJsonAsync(fx, $"/api/v1/patients/{patientA}/reports/status", token);
        Assert.Equal("Ali Alacak", json.GetProperty("patientName").GetString());
        // Çekim tedavisi Done olduğunda 36 numaralı diş "Çekilmiş" olarak işaretlenir.
        var tooth = Assert.Single(json.GetProperty("teeth").EnumerateArray());
        Assert.Equal("36", tooth.GetProperty("toothNumber").GetString());
        Assert.Equal((int)ToothCondition.Extracted, tooth.GetProperty("condition").GetInt32());
        Assert.Single(json.GetProperty("treatments").EnumerateArray());

        var pdf = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Get,
            $"/api/v1/patients/{patientA}/reports/status?format=pdf", token));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Proforma_UsesPlannedTreatments_AndRejectsForeignRecords()
    {
        var (_, token, patientA, patientB, _) = await SeedAsync();
        var definitionId = await TestApi.FindDefinitionAsync(fx, token, Extraction);

        var plannedA = await TestApi.AddTreatmentAsync(fx, token, patientA, definitionId,
            2000m, 100m, TreatmentRecordStatus.Planned, toothNumber: "26");
        var plannedB = await TestApi.AddTreatmentAsync(fx, token, patientB, definitionId,
            500m, 0m, TreatmentRecordStatus.Planned, toothNumber: "27");

        var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post,
            $"/api/v1/patients/{patientA}/reports/proforma", token, new { treatmentRecordIds = new[] { plannedA } }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proforma = await response.Content.ReadFromJsonAsync<JsonElement>();

        var line = Assert.Single(proforma.GetProperty("lines").EnumerateArray());
        Assert.Equal(1900m, line.GetProperty("lineTotal").GetDecimal());
        Assert.Equal(190m, line.GetProperty("vatAmount").GetDecimal()); // %10 KDV
        Assert.Equal(2000m, proforma.GetProperty("subTotal").GetDecimal());
        Assert.Equal(100m, proforma.GetProperty("discountTotal").GetDecimal());
        Assert.Equal(2090m, proforma.GetProperty("grandTotal").GetDecimal());
        Assert.Contains("fatura yerine geçmez", proforma.GetProperty("disclaimer").GetString());

        // Başka hastanın tedavisi teklife konulamaz (IDOR koruması).
        var foreign = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post,
            $"/api/v1/patients/{patientA}/reports/proforma", token, new { treatmentRecordIds = new[] { plannedB } }));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        // Yapılmış (Done) tedavi teklife konulamaz.
        var doneId = (await TestApi.GetJsonAsync(fx,
                $"/api/v1/patients/{patientA}/treatments?status={(int)TreatmentRecordStatus.Done}", token))
            .EnumerateArray().First().GetProperty("id").GetInt64();
        var done = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post,
            $"/api/v1/patients/{patientA}/reports/proforma", token, new { treatmentRecordIds = new[] { doneId } }));
        Assert.Equal(HttpStatusCode.BadRequest, done.StatusCode);

        var pdf = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post,
            $"/api/v1/patients/{patientA}/reports/proforma?format=pdf", token,
            new { treatmentRecordIds = new[] { plannedA } }));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task PatientReports_AreIsolatedPerTenant()
    {
        var (_, token, patientA, _, _) = await SeedAsync();
        var other = await TestApi.CreateTenantAsync(fx, "RaporYabanci");
        var otherToken = await TestApi.LoginAsync(fx, other);

        // Başka kiracının token'ı ile bu hastanın raporuna erişilemez.
        var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Get,
            $"/api/v1/patients/{patientA}/reports/treatment", otherToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Notifications_AreProducedForAppointmentsAndPayments()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Bildirim");
        var token = await TestApi.LoginAsync(fx, tenant);
        var patientId = await TestApi.CreatePatientAsync(fx, token, "Bildirim", "Hastası");

        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(7);
        var created = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/appointments", token, new
        {
            clinicId = tenant.ClinicId,
            patientId,
            doctorUserId = tenant.OwnerUserId,
            startUtc = start,
            endUtc = start.AddMinutes(30),
            type = (byte)AppointmentType.Normal,
        }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appointmentId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, $"/api/v1/appointments/{appointmentId}/status", token,
            new { status = (byte)AppointmentStatus.Cancelled, cancelReason = "Hasta erteledi" }));
        await TestApi.PayAsync(fx, token, patientId, 250m, PaymentMethod.Cash);

        var list = await TestApi.GetJsonAsync(fx, "/api/v1/notifications", token);
        var events = list.GetProperty("page").GetProperty("items").EnumerateArray()
            .Select(n => n.GetProperty("eventType").GetString()).ToList();
        Assert.Contains("appointment_created", events);
        Assert.Contains("appointment_cancelled", events);
        Assert.Contains("payment_received", events);
        Assert.Equal(3, list.GetProperty("unreadCount").GetInt32());

        // Okundu işaretleme
        var firstId = list.GetProperty("page").GetProperty("items")[0].GetProperty("id").GetInt64();
        var read = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Post, $"/api/v1/notifications/{firstId}/read", token));
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.Equal(2, (await TestApi.GetJsonAsync(fx, "/api/v1/notifications", token))
            .GetProperty("unreadCount").GetInt32());

        var readAll = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Post, "/api/v1/notifications/read-all", token));
        Assert.Equal(HttpStatusCode.OK, readAll.StatusCode);
        Assert.Equal(0, (await TestApi.GetJsonAsync(fx, "/api/v1/notifications?unreadOnly=true", token))
            .GetProperty("unreadCount").GetInt32());

        // Bildirimler kiracıya kapalıdır.
        var otherToken = await TestApi.LoginAsync(fx, await TestApi.CreateTenantAsync(fx, "BildirimYabanci"));
        Assert.Equal(0, (await TestApi.GetJsonAsync(fx, "/api/v1/notifications", otherToken))
            .GetProperty("page").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Reports_RequireReportPermission()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Yetkisiz");
        var token = await TestApi.LoginAsync(fx, tenant);

        // Owner rolünden report.view iznini kaldır → rapor ucu 403 dönmeli.
        var roles = await TestApi.GetJsonAsync(fx, "/api/v1/settings/roles", token);
        var owner = roles.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Owner");
        var permissions = owner.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString()!).Where(p => p != "report.view").ToArray();

        var update = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
            $"/api/v1/settings/roles/{owner.GetProperty("id").GetInt64()}/permissions", token, new { permissions }));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // Yeni token'da izin yok.
        var newToken = await TestApi.LoginAsync(fx, tenant);
        var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Get, "/api/v1/reports/revenue", newToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
