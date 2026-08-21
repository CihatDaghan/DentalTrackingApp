using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Patients;
using Dental.Application.Tenants;
using Dental.Application.Treatments;
using Dental.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// C aşaması "bitti" kriterleri: katalog seed'i, toplu tedavi ekleme + diş şeması işaretleri,
/// Done geçişinin ToothStatus etkisi, FDI reddi, Done silme reddi, kiracı izolasyonu.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class TreatmentTests(ApiFixture fx)
{
    private async Task<string> LoginDemoAsync()
    {
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "demo@dental.local", password = "Demo!2026" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

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

    /// <summary>Katalogdan koda göre tedavi tanımı bulur (seed'in geldiğini de doğrular).</summary>
    private async Task<long> FindDefinitionAsync(string token, string code)
    {
        var response = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/treatment-catalog?search={code}", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = page.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("code").GetString() == code);
        return item.GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task CatalogSeed_LoadsCategoriesDefinitionsAndDefaultPriceList()
    {
        var token = await LoginDemoAsync();

        // Kategoriler (11 TDB başlığı) ve en az 120 kalem tedavi tanımı seed'den gelmeli.
        var categories = await fx.Client.SendAsync(Req(HttpMethod.Get, "/api/v1/treatment-categories", token));
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        var categoryList = await categories.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(categoryList.GetArrayLength() >= 11);

        var definitions = await fx.Client.SendAsync(Req(HttpMethod.Get, "/api/v1/treatment-catalog?pageSize=1", token));
        var page = await definitions.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 120);

        // Varsayılan fiyat listesi kalemleriyle birlikte kopyalanmış olmalı.
        var priceLists = await fx.Client.SendAsync(Req(HttpMethod.Get, "/api/v1/price-lists", token));
        var priceListArr = await priceLists.Content.ReadFromJsonAsync<JsonElement>();
        var defaultList = priceListArr.EnumerateArray().Single(p => p.GetProperty("isDefault").GetBoolean());
        Assert.True(defaultList.GetProperty("itemCount").GetInt32() >= 120);

        // ICD-10 global referansı yüklü ve aranabilir.
        var icd = await fx.Client.SendAsync(Req(HttpMethod.Get, "/api/v1/icd-codes?search=K02", token));
        var icdArr = await icd.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(icdArr.EnumerateArray(), c => c.GetProperty("code").GetString() == "K02.1");
    }

    [Fact]
    public async Task AddTreatments_ListAndToothChart_Work()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Şema", "Hastası");
        var fillingId = await FindDefinitionAsync(token, "DIS-022");   // üç yüzlü kompozit (RequiresSurface)
        var rootCanalId = await FindDefinitionAsync(token, "DIS-042"); // üç kanal (effect: RootCanal)
        var scalingId = await FindDefinitionAsync(token, "DIS-100");   // detertraj (WholeMouth)

        // Yeni hastanın diş şeması boş.
        var emptyTeeth = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/teeth", token));
        Assert.Equal(HttpStatusCode.OK, emptyTeeth.StatusCode);
        var emptyDto = await emptyTeeth.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, emptyDto.GetProperty("teeth").GetArrayLength());

        // Toplu ekleme: 16 dolgu MOD Planned + 36 kanal Diagnosis + detertraj Done.
        var add = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[]
            {
                new { treatmentDefinitionId = fillingId, toothNumber = "16",
                    surfaces = (byte)(ToothSurfaces.M | ToothSurfaces.O | ToothSurfaces.D),
                    status = (byte)TreatmentRecordStatus.Planned },
                new { treatmentDefinitionId = rootCanalId, toothNumber = "36",
                    status = (byte)TreatmentRecordStatus.Diagnosis, diagnosisIcdCode = "K04.0", rootCanalCount = (byte)3 },
                new { treatmentDefinitionId = scalingId, status = (byte)TreatmentRecordStatus.Done },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var added = await add.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, added.GetArrayLength());

        // Fiyat gönderilmedi -> tarifeden çözümlendi (katalog taban fiyatı > 0).
        Assert.All(added.EnumerateArray(), r => Assert.True(r.GetProperty("price").GetDecimal() > 0));
        // Detertraj Done eklendi -> PerformedAtUtc dolu.
        var scaling = added.EnumerateArray().Single(r => r.GetProperty("status").GetInt32() == (int)TreatmentRecordStatus.Done);
        Assert.NotEqual(JsonValueKind.Null, scaling.GetProperty("performedAtUtc").ValueKind);

        // Diş şeması: 16 ve 36 işaretli, detertraj ağız geneli listesinde.
        var teeth = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/teeth", token));
        var teethDto = await teeth.Content.ReadFromJsonAsync<JsonElement>();
        var teethArr = teethDto.GetProperty("teeth").EnumerateArray().ToList();
        Assert.Equal(2, teethArr.Count);
        var tooth16 = teethArr.Single(t => t.GetProperty("toothNumber").GetString() == "16");
        var mark16 = tooth16.GetProperty("marks")[0];
        Assert.Equal((int)TreatmentRecordStatus.Planned, mark16.GetProperty("status").GetInt32());
        Assert.Equal(7, mark16.GetProperty("surfaces").GetInt32()); // M|O|D
        Assert.False(string.IsNullOrEmpty(mark16.GetProperty("categoryColorHex").GetString())); // kategori rengi
        Assert.Single(teethDto.GetProperty("mouthWideMarks").EnumerateArray());

        // Durum filtresiyle listeleme.
        var planned = await fx.Client.SendAsync(Req(HttpMethod.Get,
            $"/api/v1/patients/{patientId}/treatments?status={(byte)TreatmentRecordStatus.Planned}", token));
        var plannedArr = await planned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(plannedArr.EnumerateArray());
        Assert.Equal("16", plannedArr[0].GetProperty("toothNumber").GetString());
        Assert.False(string.IsNullOrEmpty(plannedArr[0].GetProperty("doctorName").GetString()));

        // Toplu fiyat güncelleme (Edit Price).
        var recordIds = added.EnumerateArray().Select(r => r.GetProperty("id").GetInt64()).ToArray();
        var bulk = await fx.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/treatments/bulk-price", token, new
        {
            items = new object[]
            {
                new { id = recordIds[0], price = 4000m, discountAmount = 500m },
                new { id = recordIds[1], price = 6000m },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, bulk.StatusCode);
        var bulkArr = await bulk.Content.ReadFromJsonAsync<JsonElement>();
        var updated = bulkArr.EnumerateArray().Single(r => r.GetProperty("id").GetInt64() == recordIds[0]);
        Assert.Equal(4000m, updated.GetProperty("price").GetDecimal());
        Assert.Equal(500m, updated.GetProperty("discountAmount").GetDecimal());
    }

    [Fact]
    public async Task DoneTransition_SetsPerformedAt_AndUpdatesToothStatus()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Çekim", "Hastası");
        var extractionId = await FindDefinitionAsync(token, "DIS-080"); // basit çekim (effect: Extracted)

        var add = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[]
            {
                new { treatmentDefinitionId = extractionId, toothNumber = "38",
                    status = (byte)TreatmentRecordStatus.Planned },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var added = await add.Content.ReadFromJsonAsync<JsonElement>();
        var recordId = added[0].GetProperty("id").GetInt64();

        // Planned -> Done: PerformedAtUtc set edilir, ToothStatus Extracted olur.
        var done = await fx.Client.SendAsync(Req(HttpMethod.Put, $"/api/v1/treatments/{recordId}/status", token,
            new { status = (byte)TreatmentRecordStatus.Done }));
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var doneDto = await done.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)TreatmentRecordStatus.Done, doneDto.GetProperty("status").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, doneDto.GetProperty("performedAtUtc").ValueKind);

        var teeth = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/teeth", token));
        var teethDto = await teeth.Content.ReadFromJsonAsync<JsonElement>();
        var tooth38 = teethDto.GetProperty("teeth").EnumerateArray()
            .Single(t => t.GetProperty("toothNumber").GetString() == "38");
        Assert.Equal((int)ToothCondition.Extracted, tooth38.GetProperty("condition").GetInt32());

        // Done kayıt silinemez (yalnız Cancelled'a çevrilebilir).
        var delete = await fx.Client.SendAsync(Req(HttpMethod.Delete, $"/api/v1/treatments/{recordId}", token));
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);

        // Cancelled her durumdan mümkün.
        var cancel = await fx.Client.SendAsync(Req(HttpMethod.Put, $"/api/v1/treatments/{recordId}/status", token,
            new { status = (byte)TreatmentRecordStatus.Cancelled }));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
    }

    [Fact]
    public async Task InvalidFdiToothNumber_AndMissingSurface_AreRejected()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Fdi", "Hastası");
        var fillingId = await FindDefinitionAsync(token, "DIS-020");   // dolgu (RequiresSurface, PerTooth)
        var scalingId = await FindDefinitionAsync(token, "DIS-100");   // detertraj (WholeMouth)

        // Geçersiz FDI (99) -> 400.
        var invalidTooth = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[] { new { treatmentDefinitionId = fillingId, toothNumber = "99",
                surfaces = (byte)ToothSurfaces.O, status = (byte)TreatmentRecordStatus.Planned } },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, invalidTooth.StatusCode);

        // Dolguda yüzey zorunlu -> 400.
        var noSurface = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[] { new { treatmentDefinitionId = fillingId, toothNumber = "16",
                status = (byte)TreatmentRecordStatus.Planned } },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, noSurface.StatusCode);

        // PerTooth işlemde diş no zorunlu -> 400.
        var noTooth = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[] { new { treatmentDefinitionId = fillingId,
                surfaces = (byte)ToothSurfaces.O, status = (byte)TreatmentRecordStatus.Planned } },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, noTooth.StatusCode);

        // WholeMouth işleme diş no gönderilemez -> 400.
        var toothOnWholeMouth = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[] { new { treatmentDefinitionId = scalingId, toothNumber = "16",
                status = (byte)TreatmentRecordStatus.Planned } },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, toothOnWholeMouth.StatusCode);

        // Hiçbiri kaydedilmedi.
        var list = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/treatments", token));
        var arr = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, arr.GetArrayLength());
    }

    [Fact]
    public async Task Treatments_AreIsolatedPerTenant()
    {
        long tenantA, tenantB, adminA;
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
            var a = await provisioning.CreateAsync(new CreateTenantRequest(
                "Tedavi İzolasyon A", TenantLegalType.Company, $"ta-{Guid.NewGuid():N}@t.local", "A", "Owner", "Test!2026"));
            var b = await provisioning.CreateAsync(new CreateTenantRequest(
                "Tedavi İzolasyon B", TenantLegalType.Company, $"tb-{Guid.NewGuid():N}@t.local", "B", "Owner", "Test!2026"));
            (tenantA, adminA) = (a.TenantId, a.AdminUserId);
            tenantB = b.TenantId;
        }

        var scopeFactory = fx.Services.GetRequiredService<ITenantScopeFactory>();
        long patientId;
        using (var scopeA = scopeFactory.CreateScope(tenantA))
        {
            // Provisioning yeni kiracıya kataloğu kopyalamış olmalı.
            var catalog = scopeA.ServiceProvider.GetRequiredService<ICatalogService>();
            var definitions = await catalog.ListDefinitionsAsync(new TreatmentCatalogQuery(Search: "DIS-080"));
            var extraction = Assert.Single(definitions.Items);

            var patients = scopeA.ServiceProvider.GetRequiredService<IPatientService>();
            var db = scopeA.ServiceProvider.GetRequiredService<Dental.Infrastructure.Persistence.AppDbContext>();
            var clinicId = db.Clinics.Single(c => c.TenantId == tenantA).Id;
            patientId = (await patients.CreateAsync(new PatientUpsertRequest("Gizli", "Tedavi", ClinicId: clinicId))).Id;

            var treatments = scopeA.ServiceProvider.GetRequiredService<ITreatmentService>();
            var added = await treatments.AddTreatmentsAsync(patientId, new TreatmentAddRequest(
                [new TreatmentAddItem(extraction.Id, ToothNumber: "11", DoctorUserId: adminA)]));
            Assert.Single(added);
        }

        using (var scopeB = scopeFactory.CreateScope(tenantB))
        {
            // A kiracısının hastası/tedavisi B'de görünmez.
            var treatments = scopeB.ServiceProvider.GetRequiredService<ITreatmentService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => treatments.ListTreatmentsAsync(patientId));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => treatments.GetPatientTeethAsync(patientId));

            // B kendi kataloğunu görür (şablon kopyası), A'nın kayıtlarını değil.
            var catalog = scopeB.ServiceProvider.GetRequiredService<ICatalogService>();
            var page = await catalog.ListDefinitionsAsync(new TreatmentCatalogQuery(PageSize: 1));
            Assert.True(page.TotalCount >= 120);
        }
    }
}
