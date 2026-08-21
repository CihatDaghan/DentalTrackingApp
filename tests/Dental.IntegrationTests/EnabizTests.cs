using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Enabiz;
using Dental.Application.Tenants;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Dental.Integrations.Enabiz.Fake;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// H aşaması "bitti" kriterleri: Held modunda paket üretilir ama gönderilmez, bağımlılık sırası
/// (101 kabul edilmeden 203 gitmez), taşıma hatasında artan aralıklı yeniden deneme ve
/// ManualReview, mod geçişinde geri doldurma, iş reddinin düzeltme kuyruğuna düşmesi,
/// KTS tescili olmadan Live moda geçilememesi ve kiracı izolasyonu.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class EnabizTests(ApiFixture fx)
{
    private const string DemoEmail = "demo@dental.local";
    private const string DemoPassword = "Demo!2026";

    // ---- Yardımcılar ----

    private async Task<string> LoginAsync(string email = DemoEmail, string password = DemoPassword)
    {
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
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

    private async Task<JsonElement> SendOkAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await fx.Client.SendAsync(Req(method, url, token, body));
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"{method} {url} → {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content).RootElement.Clone();
    }

    private Task<JsonElement> SetModeAsync(string token, EnabizMode mode, string? tckn = null) =>
        SendOkAsync(HttpMethod.Put, "/api/v1/enabiz/settings", token, new
        {
            mode = (byte)mode,
            ckysCode = "123456",
            ussUsername = "test-uss-user",
            ussPassword = "test-uss-pass",
            applicationCode = "TESTAPP",
        });

    /// <summary>
    /// Checksum'ı geçerli, benzersiz TCKN üretir. Sabit TCKN kullanmak diğer test süitleriyle
    /// çakışıyor ("bu TCKN ile kayıtlı hasta var"); her çağrı kendi numarasını üretir.
    /// </summary>
    private static string NewTckn()
    {
        var digits = new int[11];
        digits[0] = Random.Shared.Next(1, 10);
        for (var i = 1; i < 9; i++) digits[i] = Random.Shared.Next(0, 10);

        var odd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var even = digits[1] + digits[3] + digits[5] + digits[7];
        digits[9] = ((odd * 7 - even) % 10 + 10) % 10;
        digits[10] = digits.Take(10).Sum() % 10;
        return string.Concat(digits);
    }

    private async Task<long> CreatePatientAsync(
        string token, string tckn, string firstName = "Enabiz", string lastName = "Hasta")
    {
        var dto = await SendOkAsync(HttpMethod.Post, "/api/v1/patients", token, new
        {
            firstName,
            lastName,
            tckn,
            birthDate = "1990-05-17",
            gender = (byte)Gender.Female,
            city = "İstanbul",
            district = "Kadıköy",
        });
        return dto.GetProperty("id").GetInt64();
    }

    private async Task<long> FindDefinitionAsync(string token, string code)
    {
        var page = await SendOkAsync(HttpMethod.Get, $"/api/v1/treatment-catalog?search={code}", token);
        return page.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("code").GetString() == code)
            .GetProperty("id").GetInt64();
    }

    /// <summary>Tedaviyi doğrudan 'Yapıldı' ekler — e-Nabız tetiklemesi bu geçişte olur.</summary>
    private async Task<long> AddDoneTreatmentAsync(
        string token, long patientId, long definitionId, string? tooth = "36", string? icd = "K02.1")
    {
        var added = await SendOkAsync(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[]
            {
                new
                {
                    treatmentDefinitionId = definitionId,
                    toothNumber = tooth,
                    status = (byte)TreatmentRecordStatus.Done,
                    price = 1500m,
                    diagnosisIcdCode = icd,
                },
            },
        });
        return added[0].GetProperty("id").GetInt64();
    }

    /// <summary>Katalogda SUT kodu olan bir tedavi tanımı garanti eder (203 MUDAHALE zorunlu).</summary>
    private async Task<long> EnsureSutDefinitionAsync(string token) =>
        (await EnsureSutDefinitionWithCodeAsync(token)).DefinitionId;

    private async Task<(long DefinitionId, string SutCode)> EnsureSutDefinitionWithCodeAsync(string token)
    {
        var definitionId = await FindDefinitionAsync(token, "DIS-101");
        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var definition = await db.TreatmentDefinitions.IgnoreQueryFilters()
            .FirstAsync(d => d.Id == definitionId);
        if (string.IsNullOrWhiteSpace(definition.SutCode))
        {
            definition.SutCode = "404010";
            await db.SaveChangesAsync();
        }
        return (definitionId, definition.SutCode!);
    }

    private async Task<long> DemoTenantIdAsync()
    {
        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == DemoEmail);
        return user.TenantId ?? throw new InvalidOperationException("Demo kullanıcının kiracısı yok.");
    }

    /// <summary>Job modunda (JWT'siz) dispatcher erişimi.</summary>
    private async Task<T> InTenantScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        var tenantId = await DemoTenantIdAsync();
        var factory = fx.Services.GetRequiredService<ITenantScopeFactory>();
        using var scope = factory.CreateScope(tenantId);
        return await action(scope.ServiceProvider);
    }

    private async Task<List<JsonElement>> SubmissionsForVisitAsync(string token, long visitId)
    {
        var page = await SendOkAsync(HttpMethod.Get, "/api/v1/enabiz/submissions?pageSize=100", token);
        return [.. page.GetProperty("items").EnumerateArray()
            .Where(i => i.TryGetProperty("visitId", out var v) &&
                        v.ValueKind == JsonValueKind.Number && v.GetInt64() == visitId)];
    }

    private async Task<long> LatestVisitIdAsync(long patientId)
    {
        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Visits.IgnoreQueryFilters()
            .Where(v => v.PatientId == patientId)
            .OrderByDescending(v => v.Id)
            .Select(v => v.Id)
            .FirstAsync();
    }

    // ---- 1) Held modu: paket üretilir, GÖNDERİLMEZ ----

    [Fact]
    public async Task HeldMode_ProducesPacketsButDoesNotSend()
    {
        var token = await LoginAsync();
        await SetModeAsync(token, EnabizMode.Held);

        var definitionId = await EnsureSutDefinitionAsync(token);
        var patientId = await CreatePatientAsync(token, NewTckn());
        await AddDoneTreatmentAsync(token, patientId, definitionId);

        var visitId = await LatestVisitIdAsync(patientId);
        var submissions = await SubmissionsForVisitAsync(token, visitId);

        // 101 (hasta kayıt) + 103 (muayene) + 203 (ağız-diş) üretilmiş olmalı.
        var types = submissions.Select(s => s.GetProperty("packetType").GetInt32()).OrderBy(t => t).ToList();
        Assert.Contains((int)EnabizPacketType.HastaKayit101, types);
        Assert.Contains((int)EnabizPacketType.Muayene103, types);
        Assert.Contains((int)EnabizPacketType.AgizDisSagligi203, types);

        // Hepsi Held: gönderim YOK.
        Assert.All(submissions, s =>
            Assert.Equal((int)EnabizSubmissionState.Held, s.GetProperty("state").GetInt32()));
        Assert.All(submissions, s => Assert.Equal(JsonValueKind.Null, s.GetProperty("sentAtUtc").ValueKind));

        // Durum ekranındaki "bekleyen paket" sayacı dolmuş olmalı.
        var status = await SendOkAsync(HttpMethod.Get, "/api/v1/enabiz/status", token);
        Assert.True(status.GetProperty("heldCount").GetInt32() >= 3);
        Assert.Equal((int)EnabizMode.Held, status.GetProperty("mode").GetInt32());
    }

    [Fact]
    public async Task DisabledMode_ProducesNoPackets()
    {
        var token = await LoginAsync();
        await SetModeAsync(token, EnabizMode.Disabled);

        var definitionId = await EnsureSutDefinitionAsync(token);
        var patientId = await CreatePatientAsync(token, NewTckn());
        await AddDoneTreatmentAsync(token, patientId, definitionId);

        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var visitIds = await db.Visits.IgnoreQueryFilters()
            .Where(v => v.PatientId == patientId).Select(v => v.Id).ToListAsync();
        var count = await db.EnabizSubmissions.IgnoreQueryFilters()
            .CountAsync(s => s.VisitId != null && visitIds.Contains(s.VisitId.Value));

        Assert.Equal(0, count);
    }

    // ---- 2) Mod geçişi + geri doldurma + bağımlılık sırası ----

    [Fact]
    public async Task ModeSwitchToTestOnly_BackfillsHeldPacketsInDependencyOrder()
    {
        var token = await LoginAsync();
        await SetModeAsync(token, EnabizMode.Held);

        var definitionId = await EnsureSutDefinitionAsync(token);
        var patientId = await CreatePatientAsync(token, NewTckn());
        await AddDoneTreatmentAsync(token, patientId, definitionId);
        var visitId = await LatestVisitIdAsync(patientId);

        // Held'de bekliyor.
        Assert.All(await SubmissionsForVisitAsync(token, visitId), s =>
            Assert.Equal((int)EnabizSubmissionState.Held, s.GetProperty("state").GetInt32()));

        // Moda TestOnly'ye geçilir → geri doldurma Held'leri Queued'a çeker, ardından gönderir.
        await SetModeAsync(token, EnabizMode.TestOnly);
        var backfilled = await InTenantScopeAsync(sp =>
            sp.GetRequiredService<IEnabizDispatcher>().BackfillHeldAsync(200, CancellationToken.None));
        Assert.True(backfilled > 0);

        // Geri doldurma paketleri Held'den Queued'a çekmiş olmalı.
        var queued = await SubmissionsForVisitAsync(token, visitId);
        Assert.All(queued, s =>
            Assert.Equal((int)EnabizSubmissionState.Queued, s.GetProperty("state").GetInt32()));

        // Bu ziyaretin paketleri bağımlılık sırasıyla gönderilir (101 önce). Paylaşılan kuyruk
        // turu yerine kimliklerle gönderilir; böylece test diğer süitlerin kuyruğundan etkilenmez.
        foreach (var id in queued
            .OrderBy(s => s.GetProperty("packetType").GetInt32() == (int)EnabizPacketType.HastaKayit101 ? 0 : 1)
            .ThenBy(s => s.GetProperty("id").GetInt64())
            .Select(s => s.GetProperty("id").GetInt64()))
        {
            await InTenantScopeAsync(sp =>
                sp.GetRequiredService<IEnabizDispatcher>().DispatchAsync(id, CancellationToken.None));
        }

        var submissions = await SubmissionsForVisitAsync(token, visitId);
        Assert.All(submissions, s =>
            Assert.Equal((int)EnabizSubmissionState.Accepted, s.GetProperty("state").GetInt32()));

        // 101 SysTakipNo almış olmalı; bağımlılar da onu taşımalı.
        var packet101 = submissions.Single(s =>
            s.GetProperty("packetType").GetInt32() == (int)EnabizPacketType.HastaKayit101);
        Assert.False(string.IsNullOrWhiteSpace(packet101.GetProperty("sysTakipNo").GetString()));

        var takipNo = packet101.GetProperty("sysTakipNo").GetString()!;
        var packet203 = submissions.Single(s =>
            s.GetProperty("packetType").GetInt32() == (int)EnabizPacketType.AgizDisSagligi203);
        var detail = await SendOkAsync(HttpMethod.Get,
            $"/api/v1/enabiz/submissions/{packet203.GetProperty("id").GetInt64()}", token);
        Assert.Contains(takipNo, detail.GetProperty("payloadXml").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DependentPacket_IsNotSentBefore101Accepted()
    {
        var token = await LoginAsync();
        await SetModeAsync(token, EnabizMode.TestOnly);

        var definitionId = await EnsureSutDefinitionAsync(token);
        var patientId = await CreatePatientAsync(token, NewTckn());
        await AddDoneTreatmentAsync(token, patientId, definitionId);
        var visitId = await LatestVisitIdAsync(patientId);

        var submissions = await SubmissionsForVisitAsync(token, visitId);
        var packet203Id = submissions
            .Single(s => s.GetProperty("packetType").GetInt32() == (int)EnabizPacketType.AgizDisSagligi203)
            .GetProperty("id").GetInt64();

        // 101 henüz gönderilmeden 203 doğrudan gönderilmeye çalışılır.
        var state = await InTenantScopeAsync(sp =>
            sp.GetRequiredService<IEnabizDispatcher>().DispatchAsync(packet203Id, CancellationToken.None));

        // Gönderilmemeli: bağımlılık karşılanmadığı için kuyrukta beklemeli (hata DEĞİL).
        Assert.Equal(EnabizSubmissionState.Queued, state);
        var after = await SendOkAsync(HttpMethod.Get, $"/api/v1/enabiz/submissions/{packet203Id}", token);
        Assert.Equal(JsonValueKind.Null, after.GetProperty("sentAtUtc").ValueKind);
        Assert.Equal(0, after.GetProperty("attemptCount").GetInt32());
    }

    // ---- 3) İş reddi → düzeltme kuyruğu (yeniden DENENMEZ) ----

    [Fact]
    public async Task BusinessRejection_GoesToRejectedAndIsNotRetried()
    {
        var token = await LoginAsync();
        await SetModeAsync(token, EnabizMode.TestOnly);

        var definitionId = await EnsureSutDefinitionAsync(token);
        // Sahte sürücünün red senaryosunu tetikleyen TCKN.
        var patientId = await CreatePatientAsync(
            token, NewTckn(), "Red", FakeEnabizClient.RejectMarker);
        await AddDoneTreatmentAsync(token, patientId, definitionId);
        var visitId = await LatestVisitIdAsync(patientId);

        var packet101Id = (await SubmissionsForVisitAsync(token, visitId))
            .Single(s => s.GetProperty("packetType").GetInt32() == (int)EnabizPacketType.HastaKayit101)
            .GetProperty("id").GetInt64();

        var state = await InTenantScopeAsync(sp =>
            sp.GetRequiredService<IEnabizDispatcher>().DispatchAsync(packet101Id, CancellationToken.None));

        Assert.Equal(EnabizSubmissionState.Rejected, state);

        var detail = await SendOkAsync(HttpMethod.Get, $"/api/v1/enabiz/submissions/{packet101Id}", token);
        Assert.Equal("1001", detail.GetProperty("lastErrorCode").GetString());
        // Reddedilen paket yeniden denenmez: sonraki deneme zamanı YOKTUR.
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("nextAttemptAtUtc").ValueKind);

        // Kuyruk turu reddedilmiş paketi tekrar göndermeye çalışmamalı.
        await InTenantScopeAsync(sp =>
            sp.GetRequiredService<IEnabizDispatcher>().DispatchQueuedAsync(50, CancellationToken.None));
        var again = await SendOkAsync(HttpMethod.Get, $"/api/v1/enabiz/submissions/{packet101Id}", token);
        Assert.Equal((int)EnabizSubmissionState.Rejected, again.GetProperty("state").GetInt32());
        Assert.Equal(1, again.GetProperty("attemptCount").GetInt32());
    }

    [Fact]
    public async Task Retry_MovesRejectedPacketBackToQueue()
    {
        var token = await LoginAsync();
        await SetModeAsync(token, EnabizMode.TestOnly);

        var definitionId = await EnsureSutDefinitionAsync(token);
        var patientId = await CreatePatientAsync(
            token, NewTckn(), "Duzeltme", FakeEnabizClient.RejectMarker);
        await AddDoneTreatmentAsync(token, patientId, definitionId);
        var visitId = await LatestVisitIdAsync(patientId);
        var packet101Id = (await SubmissionsForVisitAsync(token, visitId))
            .Single(s => s.GetProperty("packetType").GetInt32() == (int)EnabizPacketType.HastaKayit101)
            .GetProperty("id").GetInt64();

        await InTenantScopeAsync(sp =>
            sp.GetRequiredService<IEnabizDispatcher>().DispatchAsync(packet101Id, CancellationToken.None));

        // Hasta verisi düzeltilir (red senaryosundan çıkar), sonra elle yeniden denenir.
        using (var scope = fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var patient = await db.Patients.IgnoreQueryFilters().FirstAsync(p => p.Id == patientId);
            patient.LastName = "Duzeltildi";
            await db.SaveChangesAsync();
        }

        var retried = await SendOkAsync(HttpMethod.Post,
            $"/api/v1/enabiz/submissions/{packet101Id}/retry", token);

        // RegenerateOnSend sayesinde paket güncel hasta verisiyle yeniden üretilir ve kabul edilir.
        Assert.Equal((int)EnabizSubmissionState.Accepted, retried.GetProperty("state").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(retried.GetProperty("sysTakipNo").GetString()));
    }

    // ---- 4) Taşıma hatası → artan aralıklı yeniden deneme → ManualReview ----

    [Fact]
    public async Task TransportFailure_RetriesWithBackoffThenManualReview()
    {
        var token = await LoginAsync();
        await SetModeAsync(token, EnabizMode.TestOnly);

        var definitionId = await EnsureSutDefinitionAsync(token);
        // Sahte sürücünün taşıma hatası senaryosunu tetikleyen TCKN.
        var patientId = await CreatePatientAsync(
            token, NewTckn(), "Gecici", FakeEnabizClient.TransientFailureMarker);
        await AddDoneTreatmentAsync(token, patientId, definitionId);
        var visitId = await LatestVisitIdAsync(patientId);
        var packet101Id = (await SubmissionsForVisitAsync(token, visitId))
            .Single(s => s.GetProperty("packetType").GetInt32() == (int)EnabizPacketType.HastaKayit101)
            .GetProperty("id").GetInt64();

        // İlk deneme: geçici hata → Queued'a döner ve ileri bir zamana planlanır.
        var first = await InTenantScopeAsync(sp =>
            sp.GetRequiredService<IEnabizDispatcher>().DispatchAsync(packet101Id, CancellationToken.None));
        Assert.Equal(EnabizSubmissionState.Queued, first);

        var afterFirst = await SendOkAsync(HttpMethod.Get, $"/api/v1/enabiz/submissions/{packet101Id}", token);
        Assert.Equal(1, afterFirst.GetProperty("attemptCount").GetInt32());
        Assert.Equal("TRANSPORT", afterFirst.GetProperty("lastErrorCode").GetString());
        Assert.NotEqual(JsonValueKind.Null, afterFirst.GetProperty("nextAttemptAtUtc").ValueKind);

        // Denemeler tükenene kadar zorla: 6. denemede ManualReview.
        EnabizSubmissionState state = first;
        for (var attempt = 2; attempt <= 6; attempt++)
        {
            using (var scope = fx.Services.CreateScope())
            {
                // Bekleme süresini geçmiş sayarak sonraki denemeyi hemen mümkün kıl.
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var row = await db.EnabizSubmissions.IgnoreQueryFilters().FirstAsync(s => s.Id == packet101Id);
                row.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1);
                await db.SaveChangesAsync();
            }

            state = await InTenantScopeAsync(sp =>
                sp.GetRequiredService<IEnabizDispatcher>().DispatchAsync(packet101Id, CancellationToken.None));
        }

        Assert.Equal(EnabizSubmissionState.ManualReview, state);
        var final = await SendOkAsync(HttpMethod.Get, $"/api/v1/enabiz/submissions/{packet101Id}", token);
        Assert.Equal(6, final.GetProperty("attemptCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, final.GetProperty("nextAttemptAtUtc").ValueKind);
    }

    // ---- 5) KTS tescili olmadan Live moda geçilemez ----

    [Fact]
    public async Task LiveMode_IsRejectedWhileKtsRegistrationIsMissing()
    {
        var token = await LoginAsync();

        var response = await fx.Client.SendAsync(Req(HttpMethod.Put, "/api/v1/enabiz/settings", token, new
        {
            mode = (byte)EnabizMode.Live,
            ckysCode = "123456",
            ussUsername = "test-uss-user",
            ussPassword = "test-uss-pass",
        }));

        // Sistem düzeyi KtsRegistered bayrağı kapalı (varsayılan) → canlıya geçiş engellenir.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var status = await SendOkAsync(HttpMethod.Get, "/api/v1/enabiz/status", token);
        Assert.False(status.GetProperty("ktsRegistered").GetBoolean());
        Assert.False(status.GetProperty("canGoLive").GetBoolean());
        Assert.NotEqual((int)EnabizMode.Live, status.GetProperty("mode").GetInt32());
    }

    // ---- 6) Kiracı izolasyonu ----

    [Fact]
    public async Task Submissions_AreIsolatedBetweenTenants()
    {
        var token = await LoginAsync();
        await SetModeAsync(token, EnabizMode.Held);
        var definitionId = await EnsureSutDefinitionAsync(token);
        var patientId = await CreatePatientAsync(token, NewTckn());
        await AddDoneTreatmentAsync(token, patientId, definitionId);
        var demoVisitId = await LatestVisitIdAsync(patientId);

        // Başka bir kiracı oluşturulur ve kendi bağlamından kuyruğa bakar.
        long otherTenantId;
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
            var created = await provisioning.CreateAsync(new CreateTenantRequest(
                "Enabiz İzolasyon", TenantLegalType.Company,
                $"enabiz-{Guid.NewGuid():N}@t.local", "Test", "Owner", "Test!2026"));
            otherTenantId = created.TenantId;
        }

        var factory = fx.Services.GetRequiredService<ITenantScopeFactory>();
        using var otherScope = factory.CreateScope(otherTenantId);
        var otherService = otherScope.ServiceProvider.GetRequiredService<IEnabizService>();
        var page = await otherService.ListAsync(pageSize: 100);

        Assert.DoesNotContain(page.Items, i => i.VisitId == demoVisitId);

        // Diğer kiracının durum sayaçları da kendi verisini gösterir (demo'nunkini değil).
        var otherStatus = await otherService.GetStatusAsync();
        Assert.Equal(0, otherStatus.HeldCount);
        Assert.Equal((int)EnabizMode.Disabled, (int)otherStatus.Mode);
    }

    // ---- 7) SKRS kod seti ----

    [Fact]
    public async Task SkrsCodes_FallBackToSeedListsWithoutCredentials()
    {
        var token = await LoginAsync();

        var synced = await InTenantScopeAsync(sp =>
            sp.GetRequiredService<ISkrsCodeService>().SyncAsync(CancellationToken.None));
        Assert.True(synced > 0);

        // FDI diş kodları tohum listeden gelir.
        var teeth = await SendOkAsync(HttpMethod.Get, "/api/v1/skrs/codes?systemName=Diş&search=36", token);
        Assert.Contains(teeth.EnumerateArray(), c => c.GetProperty("code").GetString() == "36");

        // Diş hekimliğinde sık kullanılan ICD-10 tanıları da tohumlanmış olmalı.
        var diagnoses = await SendOkAsync(HttpMethod.Get, "/api/v1/skrs/codes?systemName=ICD&search=K02", token);
        Assert.Contains(diagnoses.EnumerateArray(), c => c.GetProperty("code").GetString() == "K02.1");
    }

    // ---- 8) Uçtan uca: ziyaret gönderimi ----

    [Fact]
    public async Task SendVisit_QueuesAndDispatchesWholeVisit()
    {
        var token = await LoginAsync();
        await SetModeAsync(token, EnabizMode.TestOnly);

        var (definitionId, sutCode) = await EnsureSutDefinitionWithCodeAsync(token);
        var patientId = await CreatePatientAsync(token, NewTckn());
        await AddDoneTreatmentAsync(token, patientId, definitionId, tooth: "11");
        var visitId = await LatestVisitIdAsync(patientId);

        // Uç, ziyaretin paketlerini üretip sırayla gönderir.
        var result = await SendOkAsync(HttpMethod.Post, $"/api/v1/enabiz/visits/{visitId}/send", token);
        Assert.NotEmpty(result.GetProperty("submissionIds").EnumerateArray());

        // Uç 101'i kabul ettirdi; bağımlılar için bir tur daha (kimlikle, kuyruk kalabalığından bağımsız).
        foreach (var id in result.GetProperty("submissionIds").EnumerateArray().Select(i => i.GetInt64()))
        {
            await InTenantScopeAsync(sp =>
                sp.GetRequiredService<IEnabizDispatcher>().DispatchAsync(id, CancellationToken.None));
        }

        var submissions = await SubmissionsForVisitAsync(token, visitId);
        Assert.All(submissions, s =>
            Assert.Equal((int)EnabizSubmissionState.Accepted, s.GetProperty("state").GetInt32()));

        // 203 paketi FDI diş numarasını ve SUT kodunu resmi biçimde taşımalı.
        var packet203 = submissions.Single(s =>
            s.GetProperty("packetType").GetInt32() == (int)EnabizPacketType.AgizDisSagligi203);
        var detail = await SendOkAsync(HttpMethod.Get,
            $"/api/v1/enabiz/submissions/{packet203.GetProperty("id").GetInt64()}", token);
        var xml = detail.GetProperty("payloadXml").GetString()!;

        Assert.Contains("<AGIZ_DIS_SAGLISI>", xml, StringComparison.Ordinal);
        Assert.Contains("TEDAVI_EDILEN_DISIN_KODU", xml, StringComparison.Ordinal);
        Assert.Contains("code=\"11\"", xml, StringComparison.Ordinal);
        // MUDAHALE, katalogdaki SUT/SKRS kodunu resmi kod sistemiyle taşır.
        Assert.Contains($"code=\"{sutCode}\"", xml, StringComparison.Ordinal);
        Assert.Contains("codeSystemGuid=\"c3eb10bb-27b9-6344-e043-14031b0a5679\"", xml, StringComparison.Ordinal);
    }

    // ---- 9) Yetki ----

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var response = await fx.Client.GetAsync("/api/v1/enabiz/submissions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
