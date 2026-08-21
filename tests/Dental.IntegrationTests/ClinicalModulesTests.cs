using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Tenants;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// E2 "bitti" kriterleri: reçete oluşturma + PDF + hekim doğrulaması (sekreter hekim olarak
/// reçete yazamaz), lab vakası durum geçmişi, stok eşzamanlı hareket tutarlılığı (paralel
/// çıkışlar CurrentQty'yi bozamaz), epikriz snapshot'ı ve ilaç listesinde kiracı izolasyonu
/// (kiracı özel satır diğer kiracıya görünmez, merkezi liste herkese görünür).
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class ClinicalModulesTests(ApiFixture fx)
{
    // ---- Yardımcılar ----

    private async Task<string> LoginAsync(string email, string password)
    {
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    private Task<string> LoginDemoAsync() => LoginAsync("demo@dental.local", "Demo!2026");

    private static HttpRequestMessage Req(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<long> CreatePatientAsync(string token, string firstName, string lastName)
    {
        var response = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/patients", token,
            new { firstName, lastName, phone = "905551112233" }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt64();
    }

    private async Task<long> GetDentistIdAsync(string token)
    {
        var doctors = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/doctors", token)));
        return doctors.EnumerateArray().First().GetProperty("id").GetInt64();
    }

    /// <summary>Yeni kiracı açar (provisioning şablonları kopyalar) ve owner token'ı döner.</summary>
    private async Task<string> ProvisionTenantAndLoginAsync(string clinicName)
    {
        var email = $"owner-{Guid.NewGuid():N}@t.local";
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
            await provisioning.CreateAsync(new CreateTenantRequest(
                clinicName, TenantLegalType.Company, email, "Owner", "Test", "Test!2026"));
        }
        return await LoginAsync(email, "Test!2026");
    }

    // ---- Reçete ----

    [Fact]
    public async Task Prescription_FromTemplate_GeneratesPdf_AndBecomesPrinted()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Reçete", "Hastası");
        var dentistId = await GetDentistIdAsync(token);

        // Seed'lenen şablonlar gelir; 'Çekim Sonrası' 3 kalemlidir.
        var templates = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/prescription-templates", token)));
        var template = templates.EnumerateArray()
            .First(t => t.GetProperty("name").GetString() == "Çekim Sonrası");
        Assert.Equal(3, template.GetProperty("items").GetArrayLength());
        var templateId = template.GetProperty("id").GetInt64();

        // Şablondan oluştur: kalemler kopyalanır, tenant içi RX numarası atanır.
        var create = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/patients/{patientId}/prescriptions", token,
            new { doctorUserId = dentistId, templateId }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var dto = await ReadJsonAsync(create);
        var prescriptionId = dto.GetProperty("id").GetInt64();
        Assert.StartsWith("RX-", dto.GetProperty("prescriptionNo").GetString());
        Assert.Equal((int)PrescriptionStatus.Draft, dto.GetProperty("status").GetInt32());
        Assert.Equal(3, dto.GetProperty("items").GetArrayLength());
        Assert.False(dto.GetProperty("hasControlledDrug").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("controlledWarning").ValueKind);

        // PDF: %PDF magic bytes; ilk istek üretir ve Status=Printed + PdfFileId yazar.
        var pdf = await fx.Client.SendAsync(Req(HttpMethod.Get,
            $"/api/v1/prescriptions/{prescriptionId}/pdf", token));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), pdfBytes[..4]);

        var after = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/prescriptions/{prescriptionId}", token)));
        Assert.Equal((int)PrescriptionStatus.Printed, after.GetProperty("status").GetInt32());
        Assert.True(after.GetProperty("pdfFileId").GetInt64() > 0);

        // Hasta listesinde görünür.
        var list = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/prescriptions", token)));
        Assert.Contains(list.EnumerateArray(), p => p.GetProperty("id").GetInt64() == prescriptionId);
    }

    [Fact]
    public async Task Prescription_WithControlledDrug_ReturnsWarning()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Kontrollü", "Reçete");
        var dentistId = await GetDentistIdAsync(token);

        var drugs = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/drugs?search=kodein", token)));
        var controlled = drugs.EnumerateArray().First(d => d.GetProperty("isControlled").GetBoolean());

        var create = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/patients/{patientId}/prescriptions", token,
            new
            {
                doctorUserId = dentistId,
                items = new object[] { new { drugId = controlled.GetProperty("id").GetInt64(), boxCount = 1 } },
            }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var dto = await ReadJsonAsync(create);
        Assert.True(dto.GetProperty("hasControlledDrug").GetBoolean());
        Assert.Contains("Renkli Reçete", dto.GetProperty("controlledWarning").GetString());
    }

    [Fact]
    public async Task Prescription_DoctorMustBeDentist_SecretaryRejected()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Hekimsiz", "Reçete");

        // Demo sekreterinin kullanıcı id'si (API'de personel listesi ucu yok; DB'den okunur).
        long secretaryId;
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            var db = scope.ServiceProvider.GetRequiredService<Dental.Infrastructure.Persistence.AppDbContext>();
            secretaryId = await db.Users.IgnoreQueryFilters()
                .Where(u => u.UserType == UserType.Secretary && u.TenantId != null)
                .Select(u => u.Id).FirstAsync();
        }

        var templates = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/prescription-templates", token)));
        var templateId = templates.EnumerateArray().First().GetProperty("id").GetInt64();

        // Sekreter hekim olarak reçete yazamaz → 400.
        var create = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/patients/{patientId}/prescriptions", token,
            new { doctorUserId = secretaryId, templateId }));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    // ---- Laboratuvar ----

    [Fact]
    public async Task LabCase_StatusTransitions_AreRecordedInHistory()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Lab", "Hastası");
        var dentistId = await GetDentistIdAsync(token);

        var lab = await ReadJsonAsync(await fx.Client.SendAsync(Req(HttpMethod.Post,
            "/api/v1/laboratories", token,
            new { name = $"Test Lab {Guid.NewGuid():N}", phone = "+903120001122" })));
        var laboratoryId = lab.GetProperty("id").GetInt64();

        // Vade geçmişte → gecikmiş bayrağı sorgu bazlı üretilir.
        var create = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/lab-cases", token, new
        {
            patientId,
            doctorUserId = dentistId,
            laboratoryId,
            workType = "Zirkonyum Kron",
            teethCsv = "11, 12",
            shade = "A2",
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("O"),
            price = 1500,
        }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var dto = await ReadJsonAsync(create);
        var caseId = dto.GetProperty("id").GetInt64();
        Assert.StartsWith("LAB-", dto.GetProperty("caseNo").GetString());
        Assert.Equal("11,12", dto.GetProperty("teethCsv").GetString()); // normalize edilir
        Assert.Equal((int)LabCaseStatus.Draft, dto.GetProperty("status").GetInt32());
        Assert.True(dto.GetProperty("isOverdue").GetBoolean());

        // Sent → SentDate damgalanır; InLab → geçmişe üçüncü satır düşer.
        var sent = await ReadJsonAsync(await fx.Client.SendAsync(Req(HttpMethod.Put,
            $"/api/v1/lab-cases/{caseId}/status", token,
            new { status = (int)LabCaseStatus.Sent, note = "Kargoya verildi" })));
        Assert.Equal((int)LabCaseStatus.Sent, sent.GetProperty("status").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, sent.GetProperty("sentDate").ValueKind);

        var inLab = await ReadJsonAsync(await fx.Client.SendAsync(Req(HttpMethod.Put,
            $"/api/v1/lab-cases/{caseId}/status", token, new { status = (int)LabCaseStatus.InLab })));
        Assert.True(inLab.GetProperty("isOverdue").GetBoolean()); // Status < Received olduğu sürece gecikmiş

        var history = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/lab-cases/{caseId}/history", token)));
        var statuses = history.EnumerateArray().Select(h => h.GetProperty("status").GetInt32()).ToList();
        Assert.Equal([(int)LabCaseStatus.Draft, (int)LabCaseStatus.Sent, (int)LabCaseStatus.InLab], statuses);
        Assert.Equal("Kargoya verildi",
            history.EnumerateArray().ElementAt(1).GetProperty("note").GetString());

        // Aynı duruma tekrar geçiş reddedilir.
        var repeat = await fx.Client.SendAsync(Req(HttpMethod.Put,
            $"/api/v1/lab-cases/{caseId}/status", token, new { status = (int)LabCaseStatus.InLab }));
        Assert.Equal(HttpStatusCode.BadRequest, repeat.StatusCode);

        // Gecikmiş filtresi vakayı içerir; hasta bazlı liste de döner.
        var overdue = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/lab-cases?overdueOnly=true&pageSize=100", token)));
        Assert.Contains(overdue.GetProperty("items").EnumerateArray(),
            c => c.GetProperty("id").GetInt64() == caseId);
        var forPatient = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/lab-cases", token)));
        Assert.Single(forPatient.EnumerateArray());
    }

    // ---- Stok ----

    [Fact]
    public async Task Stock_ConcurrentOuts_KeepCurrentQtyConsistent_AndAdjustmentSetsAbsolute()
    {
        var token = await LoginDemoAsync();

        var category = await ReadJsonAsync(await fx.Client.SendAsync(Req(HttpMethod.Post,
            "/api/v1/stock-categories", token, new { name = $"Sarf {Guid.NewGuid():N}" })));
        var item = await ReadJsonAsync(await fx.Client.SendAsync(Req(HttpMethod.Post,
            "/api/v1/stock-items", token, new
            {
                categoryId = category.GetProperty("id").GetInt64(),
                name = $"Eldiven {Guid.NewGuid():N}",
                unit = "kutu",
                minQty = 6,
            })));
        var itemId = item.GetProperty("id").GetInt64();

        // Giriş 10 (Purchase + birim fiyat → LastPurchasePrice güncellenir).
        var inResult = await ReadJsonAsync(await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/stock-items/{itemId}/movements", token,
            new { direction = (int)StockMovementDirection.In, qty = 10, refType = (int)StockMovementRefType.Purchase, unitCost = 25.5 })));
        Assert.Equal(10m, inResult.GetProperty("currentQty").GetDecimal());
        Assert.Equal(25.5m, inResult.GetProperty("lastPurchasePrice").GetDecimal());

        // 2 paralel çıkış (3'er): atomik UPDATE sayesinde kayıp güncelleme olmaz → 10-3-3 = 4.
        var out1 = fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/stock-items/{itemId}/movements", token,
            new { direction = (int)StockMovementDirection.Out, qty = 3, refType = (int)StockMovementRefType.TreatmentUse }));
        var out2 = fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/stock-items/{itemId}/movements", token,
            new { direction = (int)StockMovementDirection.Out, qty = 3, refType = (int)StockMovementRefType.TreatmentUse }));
        var results = await Task.WhenAll(out1, out2);
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var afterOuts = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/stock-items/{itemId}", token)));
        Assert.Equal(4m, afterOuts.GetProperty("currentQty").GetDecimal());
        Assert.True(afterOuts.GetProperty("isLow").GetBoolean()); // 4 <= 6

        // Düşük stok ucunda görünür (dashboard sayacı).
        var low = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/stock-items/low", token)));
        Assert.Contains(low.EnumerateArray(), i => i.GetProperty("id").GetInt64() == itemId);

        // Sayım düzeltmesi: Qty = yeni MUTLAK değer (7); harekete fark (+3) yazılır.
        var adjusted = await ReadJsonAsync(await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/stock-items/{itemId}/movements", token,
            new { direction = (int)StockMovementDirection.Adjustment, qty = 7, refType = (int)StockMovementRefType.Count })));
        Assert.Equal(7m, adjusted.GetProperty("currentQty").GetDecimal());

        var movements = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/stock-items/{itemId}/movements", token)));
        Assert.Equal(4, movements.GetArrayLength()); // In + 2 Out + Adjustment
        var adjustment = movements.EnumerateArray()
            .First(m => m.GetProperty("direction").GetInt32() == (int)StockMovementDirection.Adjustment);
        Assert.Equal(3m, adjustment.GetProperty("qty").GetDecimal()); // 4 → 7 farkı

        // Yetersiz stok: mevcut miktarı aşan çıkış 400 (eksiye düşmez).
        var tooMuch = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/stock-items/{itemId}/movements", token,
            new { direction = (int)StockMovementDirection.Out, qty = 99, refType = (int)StockMovementRefType.Waste }));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
    }

    // ---- Epikriz ----

    [Fact]
    public async Task Epicrisis_SnapshotsTreatments_AndStreamsPdf()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Epikriz", "Hastası");
        var dentistId = await GetDentistIdAsync(token);

        // Ağız geneli bir işlem ekle (katalogdan ilk kayıt: muayene).
        var catalog = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/treatment-catalog?pageSize=1", token)));
        var definitionId = catalog.GetProperty("items").EnumerateArray().First().GetProperty("id").GetInt64();
        var treatments = await ReadJsonAsync(await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/patients/{patientId}/treatments", token,
            new { items = new object[] { new { treatmentDefinitionId = definitionId, doctorUserId = dentistId } } })));
        var treatmentId = treatments.EnumerateArray().First().GetProperty("id").GetInt64();

        var create = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/patients/{patientId}/epicrisis", token, new
            {
                doctorUserId = dentistId,
                title = "Tedavi Sonu Epikrizi",
                diagnoses = new object[] { new { code = "K04.7", name = "Periapikal apse" } },
                treatmentRecordIds = new[] { treatmentId },
                bodyText = "Tedavi tamamlandı; 6 ay sonra kontrol önerilir.",
            }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var dto = await ReadJsonAsync(create);
        var epicrisisId = dto.GetProperty("id").GetInt64();

        // Snapshot: tedavi özet satırı (id + işlem adı + hekim adı) belgeye sabitlenmiştir.
        var line = Assert.Single(dto.GetProperty("treatments").EnumerateArray());
        Assert.Equal(treatmentId, line.GetProperty("id").GetInt64());
        Assert.False(string.IsNullOrEmpty(line.GetProperty("name").GetString()));
        Assert.False(string.IsNullOrEmpty(line.GetProperty("doctorName").GetString()));
        var diagnosis = Assert.Single(dto.GetProperty("diagnoses").EnumerateArray());
        Assert.Equal("K04.7", diagnosis.GetProperty("code").GetString());

        // Başka hastanın tedavi id'si ile epikriz açılamaz (404).
        var otherPatient = await CreatePatientAsync(token, "Başka", "Hasta");
        var wrong = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/patients/{otherPatient}/epicrisis", token, new
            {
                doctorUserId = dentistId,
                title = "Hatalı",
                diagnoses = Array.Empty<object>(),
                treatmentRecordIds = new[] { treatmentId },
            }));
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);

        // PDF akışı + PdfFileId kalıcı.
        var pdf = await fx.Client.SendAsync(Req(HttpMethod.Get,
            $"/api/v1/epicrisis/{epicrisisId}/pdf", token));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), pdfBytes[..4]);
        var after = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/epicrisis/{epicrisisId}", token)));
        Assert.True(after.GetProperty("pdfFileId").GetInt64() > 0);
    }

    // ---- Kiracı izolasyonu (ilaç listesi) ----

    [Fact]
    public async Task Drugs_TenantSpecificRowIsIsolated_GlobalListVisibleToAll()
    {
        var tokenA = await ProvisionTenantAndLoginAsync("İlaç İzolasyon A");
        var tokenB = await ProvisionTenantAndLoginAsync("İlaç İzolasyon B");

        // A kiracıya özel ilaç ekler.
        var name = $"Özel Majistral {Guid.NewGuid():N}";
        var create = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/drugs", tokenA,
            new { name, form = "Solüsyon", defaultUsage = "2x1" }));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var drug = await ReadJsonAsync(create);
        Assert.NotEqual(JsonValueKind.Null, drug.GetProperty("tenantId").ValueKind); // kiracı satırı

        // A kendi satırını görür; B görmez.
        var searchA = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/drugs?search={Uri.EscapeDataString(name)}", tokenA)));
        Assert.Single(searchA.EnumerateArray());
        var searchB = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/drugs?search={Uri.EscapeDataString(name)}", tokenB)));
        Assert.Empty(searchB.EnumerateArray());

        // Merkezi (TenantId NULL) listeyi ikisi de görür.
        foreach (var token in new[] { tokenA, tokenB })
        {
            var global = await ReadJsonAsync(await fx.Client.SendAsync(
                Req(HttpMethod.Get, "/api/v1/drugs?search=amoksisilin", token)));
            Assert.True(global.GetArrayLength() > 0);
            Assert.All(global.EnumerateArray(),
                d => Assert.Equal(JsonValueKind.Null, d.GetProperty("tenantId").ValueKind));
        }

        // B, A'nın özel ilacını reçete kalemi olarak da kullanamaz (404).
        var patientB = await CreatePatientAsync(tokenB, "İzole", "Hasta");
        long dentistB;
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            var db = scope.ServiceProvider.GetRequiredService<Dental.Infrastructure.Persistence.AppDbContext>();
            // B kiracısında hekim yok; owner'ı Dentist'e çevirmek yerine hekim kullanıcı ekle.
            // Kiracı id'si az önce açılan hastadan deterministik çözülür.
            var tenantId = await db.Patients.IgnoreQueryFilters()
                .Where(p => p.Id == patientB).Select(p => p.TenantId).FirstAsync();
            var dentist = new Dental.Domain.Entities.AppUser
            {
                TenantId = tenantId,
                UserName = $"dr-{Guid.NewGuid():N}@t.local",
                Email = $"dr-{Guid.NewGuid():N}@t.local",
                FirstName = "Test",
                LastName = "Hekim",
                UserType = UserType.Dentist,
                EmailConfirmed = true,
            };
            db.Users.Add(dentist);
            await db.SaveChangesAsync();
            dentistB = dentist.Id;
        }
        var foreign = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/patients/{patientB}/prescriptions", tokenB, new
            {
                doctorUserId = dentistB,
                items = new object[] { new { drugId = drug.GetProperty("id").GetInt64(), boxCount = 1 } },
            }));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
