using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Finance;
using Dental.Application.Patients;
using Dental.Application.Tenants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// D aşaması "bitti" kriterleri: borçlan-öde-bakiye zinciri, eşzamanlı tahsilatta bakiye
/// tutarlılığı ((c)-10 düzeltmesi), Done→Cancelled ters kayıt, taksit FIFO dağıtımı,
/// kiracı izolasyonu, sekreterin payment.delete alamaması.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class FinanceTests(ApiFixture fx)
{
    private async Task<string> LoginAsync(string email, string password)
    {
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private Task<string> LoginDemoAsync() => LoginAsync("demo@dental.local", "Demo!2026");

    private static HttpRequestMessage Req(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<long> CreatePatientAsync(string token, string firstName, string lastName)
    {
        var response = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/patients", token,
            new { firstName, lastName }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        return dto.GetProperty("id").GetInt64();
    }

    private async Task<long> FindDefinitionAsync(string token, string code)
    {
        var response = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/treatment-catalog?search={code}", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        return page.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("code").GetString() == code)
            .GetProperty("id").GetInt64();
    }

    /// <summary>Tedavi ekler ve kayıt id'sini döner.</summary>
    private async Task<long> AddTreatmentAsync(string token, long patientId, long definitionId,
        decimal price, decimal discount = 0m, TreatmentRecordStatus status = TreatmentRecordStatus.Planned)
    {
        var add = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[]
            {
                new { treatmentDefinitionId = definitionId, toothNumber = "38",
                    status = (byte)status, price, discountAmount = discount },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var added = await add.Content.ReadFromJsonAsync<JsonElement>();
        return added[0].GetProperty("id").GetInt64();
    }

    private async Task<JsonElement> GetLedgerAsync(string token, long patientId)
    {
        var response = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/ledger", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task ChargePayChain_LedgerAndBalance_AreConsistent()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Cari", "Zinciri");
        var extractionId = await FindDefinitionAsync(token, "DIS-080");
        var recordId = await AddTreatmentAsync(token, patientId, extractionId, price: 3000m, discount: 500m);

        // Planned aşamasında borç yok.
        var before = await GetLedgerAsync(token, patientId);
        Assert.Equal(0, before.GetProperty("lines").GetArrayLength());

        // Done → TreatmentCharge (Debit = Price - Discount = 2500) + bakiye artar.
        var done = await fx.Client.SendAsync(Req(HttpMethod.Put, $"/api/v1/treatments/{recordId}/status", token,
            new { status = (byte)TreatmentRecordStatus.Done }));
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        var charged = await GetLedgerAsync(token, patientId);
        var chargeLine = Assert.Single(charged.GetProperty("lines").EnumerateArray());
        Assert.Equal((int)LedgerEntryType.TreatmentCharge, chargeLine.GetProperty("entryType").GetInt32());
        Assert.Equal(2500m, chargeLine.GetProperty("debit").GetDecimal());
        Assert.Equal(2500m, charged.GetProperty("summary").GetProperty("balance").GetDecimal());

        // Tahsilat 1000 → bakiye 1500; koşan bakiye [2500, 1500]; özet doğru.
        var pay = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/payments", token,
            new { amount = 1000m, method = (byte)PaymentMethod.Cash, patientId }));
        Assert.Equal(HttpStatusCode.Created, pay.StatusCode);
        var payment = await pay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payment.GetProperty("ledgerEntryId").GetInt64() > 0);

        var after = await GetLedgerAsync(token, patientId);
        var lines = after.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal([2500m, 1500m], lines.Select(l => l.GetProperty("runningBalance").GetDecimal()));
        var summary = after.GetProperty("summary");
        Assert.Equal(2500m, summary.GetProperty("totalTreatment").GetDecimal());
        Assert.Equal(1000m, summary.GetProperty("totalPayment").GetDecimal());
        Assert.Equal(1500m, summary.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task ConcurrentPayments_BalanceStaysConsistent()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Eşzamanlı", "Tahsilat");
        var extractionId = await FindDefinitionAsync(token, "DIS-080");
        await AddTreatmentAsync(token, patientId, extractionId, price: 1000m, status: TreatmentRecordStatus.Done);

        // (c)-10 yarış koşulu: iki paralel tahsilat; bakiye = 1000 - 250 - 250 = 500 olmalı.
        var results = await Task.WhenAll(
            fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/payments", token,
                new { amount = 250m, method = (byte)PaymentMethod.Cash, patientId })),
            fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/payments", token,
                new { amount = 250m, method = (byte)PaymentMethod.BankTransfer, patientId })));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var ledger = await GetLedgerAsync(token, patientId);
        Assert.Equal(500m, ledger.GetProperty("summary").GetProperty("balance").GetDecimal());
        // Denormalize bakiye defterle tutarlı: SUM(Debit - Credit) = Balance.
        var net = ledger.GetProperty("lines").EnumerateArray()
            .Sum(l => l.GetProperty("debit").GetDecimal() - l.GetProperty("credit").GetDecimal());
        Assert.Equal(500m, net);
    }

    [Fact]
    public async Task DoneToCancelled_WritesReversalEntry()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "İptal", "Tersi");
        var extractionId = await FindDefinitionAsync(token, "DIS-080");
        var recordId = await AddTreatmentAsync(token, patientId, extractionId, price: 800m,
            status: TreatmentRecordStatus.Done);

        var charged = await GetLedgerAsync(token, patientId);
        Assert.Equal(800m, charged.GetProperty("summary").GetProperty("balance").GetDecimal());

        var cancel = await fx.Client.SendAsync(Req(HttpMethod.Put, $"/api/v1/treatments/{recordId}/status", token,
            new { status = (byte)TreatmentRecordStatus.Cancelled }));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var ledger = await GetLedgerAsync(token, patientId);
        var lines = ledger.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        var reversal = lines[^1];
        Assert.Equal((int)LedgerEntryType.Correction, reversal.GetProperty("entryType").GetInt32());
        Assert.Equal(800m, reversal.GetProperty("credit").GetDecimal());
        Assert.Equal(0m, ledger.GetProperty("summary").GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task PaymentPlan_PaymentIsAllocatedToInstallments_Fifo()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Taksit", "Planı");

        // 900 / 3 taksit = 300'er; vadeler aylık.
        var create = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/payment-plans", token, new
        {
            patientId,
            totalAmount = 900m,
            installmentCount = 3,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
        }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var plan = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([300m, 300m, 300m], plan.GetProperty("installments").EnumerateArray()
            .Select(i => i.GetProperty("amount").GetDecimal()));

        // 450 tahsilat → FIFO: 1. taksit Paid(300), 2. taksit Partial(150), 3. Pending.
        var pay = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/payments", token,
            new { amount = 450m, method = (byte)PaymentMethod.Cash, patientId }));
        Assert.Equal(HttpStatusCode.Created, pay.StatusCode);

        var list = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/payment-plans", token));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var plans = await list.Content.ReadFromJsonAsync<JsonElement>();
        var installments = plans[0].GetProperty("installments").EnumerateArray().ToList();

        Assert.Equal(450m, plans[0].GetProperty("paidAmount").GetDecimal());
        Assert.Equal(300m, installments[0].GetProperty("paidAmount").GetDecimal());
        Assert.Equal((int)InstallmentStatus.Paid, installments[0].GetProperty("status").GetInt32());
        Assert.Equal(150m, installments[1].GetProperty("paidAmount").GetDecimal());
        Assert.Equal((int)InstallmentStatus.Partial, installments[1].GetProperty("status").GetInt32());
        Assert.Equal(0m, installments[2].GetProperty("paidAmount").GetDecimal());
        Assert.Equal((int)InstallmentStatus.Pending, installments[2].GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Secretary_CanCreateButCannotDeletePayment()
    {
        var ownerToken = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(ownerToken, "Yetki", "Denemesi");
        var secretaryEmail = $"sekreter-{Guid.NewGuid():N}@t.local";

        // Demo kiracıya Secretary rollü kullanıcı ekle (provisioning'in kopyaladığı sistem rolü).
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            var db = scope.ServiceProvider.GetRequiredService<Dental.Infrastructure.Persistence.AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            var tenantId = (await db.Users.IgnoreQueryFilters()
                .SingleAsync(u => u.Email == "demo@dental.local")).TenantId!.Value;
            var clinicId = (await db.Clinics.IgnoreQueryFilters().FirstAsync(c => c.TenantId == tenantId)).Id;
            var role = await db.Roles.IgnoreQueryFilters()
                .SingleAsync(r => r.TenantId == tenantId && r.Name == "Secretary");

            var secretary = new AppUser
            {
                TenantId = tenantId,
                UserName = secretaryEmail,
                Email = secretaryEmail,
                FirstName = "Sekreter",
                LastName = "Test",
                UserType = UserType.Secretary,
                EmailConfirmed = true,
            };
            Assert.True((await userManager.CreateAsync(secretary, "Test!2026")).Succeeded);
            db.UserRoles.Add(new UserRole { UserId = secretary.Id, RoleId = role.Id });
            db.UserClinics.Add(new UserClinic { UserId = secretary.Id, ClinicId = clinicId, IsDefault = true });
            await db.SaveChangesAsync();
        }

        var secretaryToken = await LoginAsync(secretaryEmail, "Test!2026");

        // Sekreter tahsilat OLUŞTURABİLİR (payment.create var)...
        var pay = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/payments", secretaryToken,
            new { amount = 100m, method = (byte)PaymentMethod.Cash, patientId }));
        Assert.Equal(HttpStatusCode.Created, pay.StatusCode);
        var paymentId = (await pay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        // ... ama SİLEMEZ (payment.delete yok) → 403.
        var forbidden = await fx.Client.SendAsync(
            Req(HttpMethod.Delete, $"/api/v1/payments/{paymentId}", secretaryToken));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Owner silebilir; ters kayıt bakiyeyi düzeltir (100 tahsilat geri alınır).
        var deleted = await fx.Client.SendAsync(
            Req(HttpMethod.Delete, $"/api/v1/payments/{paymentId}", ownerToken));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var ledger = await GetLedgerAsync(ownerToken, patientId);
        Assert.Equal(0m, ledger.GetProperty("summary").GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task Ledger_IsIsolatedPerTenant()
    {
        long tenantA, tenantB, clinicA;
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
            var a = await provisioning.CreateAsync(new CreateTenantRequest(
                "Finans İzolasyon A", TenantLegalType.Company, $"fa-{Guid.NewGuid():N}@t.local", "A", "Owner", "Test!2026"));
            var b = await provisioning.CreateAsync(new CreateTenantRequest(
                "Finans İzolasyon B", TenantLegalType.Company, $"fb-{Guid.NewGuid():N}@t.local", "B", "Owner", "Test!2026"));
            (tenantA, clinicA) = (a.TenantId, a.ClinicId);
            tenantB = b.TenantId;
        }

        var scopeFactory = fx.Services.GetRequiredService<ITenantScopeFactory>();
        long patientId;
        using (var scopeA = scopeFactory.CreateScope(tenantA))
        {
            var patients = scopeA.ServiceProvider.GetRequiredService<IPatientService>();
            patientId = (await patients.CreateAsync(new PatientUpsertRequest("Gizli", "Cari", ClinicId: clinicA))).Id;

            var ledger = scopeA.ServiceProvider.GetRequiredService<ILedgerService>();
            await ledger.AddEntryAsync(new LedgerEntryCreateRequest(
                LedgerAccountType.Patient, patientId, null,
                LedgerEntryType.OpeningBalance, Debit: 150m, Credit: 0m, "Devir"));

            var statement = await ledger.GetStatementAsync(
                new LedgerStatementQuery(LedgerAccountType.Patient, patientId));
            Assert.Equal(150m, statement.Summary.Balance);
        }

        using (var scopeB = scopeFactory.CreateScope(tenantB))
        {
            // A kiracısının hastası/ekstresi B'de görünmez.
            var ledger = scopeB.ServiceProvider.GetRequiredService<ILedgerService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => ledger.GetStatementAsync(
                new LedgerStatementQuery(LedgerAccountType.Patient, patientId)));

            var db = scopeB.ServiceProvider.GetRequiredService<Dental.Infrastructure.Persistence.AppDbContext>();
            Assert.False(await db.LedgerEntries.AnyAsync(e => e.PatientId == patientId));
        }
    }
}
