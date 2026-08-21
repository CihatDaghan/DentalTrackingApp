using System.Net;
using System.Net.Http.Headers;
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
/// E1 "bitti" kriterleri: anamnez doldurma + kritik rozet, medya yükleme/indirme + kiracı
/// izolasyonu (IDOR), onam yaşam döngüsü (create → send-sms → public get → public sign → 410;
/// süresi dolmuş token 410) ve şablon HTML sanitizasyonu ((c)-7).
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class ClinicalRecordsTests(ApiFixture fx)
{
    /// <summary>1x1 kırmızı PNG (test imzası/görseli).</summary>
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
    private static byte[] TinyPng => Convert.FromBase64String(TinyPngBase64);

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

    private async Task<long> UploadPngAsync(string token, long patientId)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(TinyPng);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "test.png");
        content.Add(new StringContent(nameof(MediaCategory.IntraoralPhoto)), "category");
        content.Add(new StringContent("Ağız içi test görseli"), "description");
        content.Add(new StringContent("11"), "toothNumber");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/patients/{patientId}/media")
        {
            Content = content,
        };
        request.Headers.Authorization = new("Bearer", token);
        var response = await fx.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await ReadJsonAsync(response);
        Assert.True(dto.GetProperty("hasThumbnail").GetBoolean()); // görüntüde 320px thumbnail üretilir
        Assert.Equal(64, dto.GetProperty("sha256").GetString()!.Length);
        return dto.GetProperty("id").GetInt64();
    }

    private async Task<(long ConsentId, Guid Token)> CreateConsentAndSendSmsAsync(string token, long patientId)
    {
        var templates = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/consent-templates", token)));
        var templateId = templates.EnumerateArray().First().GetProperty("id").GetInt64();

        var create = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/consents",
            token, new { templateId }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var consentId = (await ReadJsonAsync(create)).GetProperty("id").GetInt64();

        var sms = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/consents/{consentId}/send-sms", token));
        Assert.Equal(HttpStatusCode.OK, sms.StatusCode);
        var smsBody = await ReadJsonAsync(sms);
        var publicUrl = smsBody.GetProperty("publicUrl").GetString()!;
        var signToken = Guid.Parse(publicUrl[(publicUrl.LastIndexOf('/') + 1)..]);
        return (consentId, signToken);
    }

    // ---- Anamnez ----

    [Fact]
    public async Task AnamnesisFill_IsVersioned_AndCriticalAnswersProduceBadge()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Anamnez", "Hastası");

        // Seed'lenen varsayılan şablon gelir.
        var templates = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, "/api/v1/anamnesis-templates", token)));
        var defaultTemplate = templates.EnumerateArray().Single(t => t.GetProperty("isDefault").GetBoolean());
        var templateId = defaultTemplate.GetProperty("id").GetInt64();
        Assert.True(defaultTemplate.GetProperty("questionCount").GetInt32() >= 20);

        var detail = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/anamnesis-templates/{templateId}", token)));
        var questions = detail.GetProperty("questions").EnumerateArray().ToList();
        var allergyQ = questions.First(q =>
            q.GetProperty("isCritical").GetBoolean() &&
            q.GetProperty("questionText").GetString()!.Contains("alerji"));
        var nonCriticalQ = questions.First(q => !q.GetProperty("isCritical").GetBoolean());

        // 1. doldurma: alerji=EVET (kritik) → rozet beklenir.
        var fill = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/anamnesis", token,
            new
            {
                templateId,
                answers = new object[]
                {
                    new { questionId = allergyQ.GetProperty("id").GetInt64(), boolValue = true, textValue = "Penisilin" },
                    new { questionId = nonCriticalQ.GetProperty("id").GetInt64(), boolValue = false },
                },
            }));
        Assert.Equal(HttpStatusCode.OK, fill.StatusCode);

        var flags = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/critical-flags", token)));
        var flag = Assert.Single(flags.EnumerateArray());
        Assert.Equal("Penisilin", flag.GetProperty("textValue").GetString());

        // 2. doldurma (versiyonlu): alerji=HAYIR → rozet kalkar, geçmiş yanıt korunur.
        var refill = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/patients/{patientId}/anamnesis", token,
            new
            {
                templateId,
                answers = new object[]
                {
                    new { questionId = allergyQ.GetProperty("id").GetInt64(), boolValue = false },
                },
            }));
        Assert.Equal(HttpStatusCode.OK, refill.StatusCode);

        var responses = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/anamnesis", token)));
        Assert.Equal(2, responses.GetArrayLength());

        var flagsAfter = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/patients/{patientId}/critical-flags", token)));
        Assert.Empty(flagsAfter.EnumerateArray());
    }

    // ---- Medya ----

    [Fact]
    public async Task MediaUpload_ProducesThumbnail_AndDownloadStreamsOriginal()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Medya", "Hastası");
        var mediaId = await UploadPngAsync(token, patientId);

        // Liste: kategori filtresiyle gelir.
        var list = await ReadJsonAsync(await fx.Client.SendAsync(Req(HttpMethod.Get,
            $"/api/v1/patients/{patientId}/media?category={nameof(MediaCategory.IntraoralPhoto)}", token)));
        Assert.Contains(list.EnumerateArray(), m => m.GetProperty("id").GetInt64() == mediaId);

        // İndirme: birebir aynı içerik + attachment başlığı.
        var download = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/media/{mediaId}/download", token));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(TinyPng, await download.Content.ReadAsByteArrayAsync());

        // Thumbnail JPEG döner.
        var thumb = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/media/{mediaId}/thumbnail", token));
        Assert.Equal(HttpStatusCode.OK, thumb.StatusCode);
        Assert.Equal("image/jpeg", thumb.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Media_IsIsolatedPerTenant_ForeignTokenGets404()
    {
        var tokenA = await ProvisionTenantAndLoginAsync("Medya İzolasyon A");
        var tokenB = await ProvisionTenantAndLoginAsync("Medya İzolasyon B");

        var patientA = await CreatePatientAsync(tokenA, "Gizli", "Görsel");
        var mediaId = await UploadPngAsync(tokenA, patientA);

        // (c)-14 IDOR: A'nın medyası B token'ıyla 404 (403 değil — varlık sızdırılmaz).
        var download = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/media/{mediaId}/download", tokenB));
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        var delete = await fx.Client.SendAsync(Req(HttpMethod.Delete, $"/api/v1/media/{mediaId}", tokenB));
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        // Sahibi indirebilir.
        var ownerDownload = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/media/{mediaId}/download", tokenA));
        Assert.Equal(HttpStatusCode.OK, ownerDownload.StatusCode);
    }

    // ---- Onam ----

    [Fact]
    public async Task ConsentLifecycle_SmsThenPublicSign_TokenIsSingleUse()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Onam", "Hastası");
        var (consentId, signToken) = await CreateConsentAndSendSmsAsync(token, patientId);

        // Public GET auth'suz çalışır; durum SentBySms. Ad, onam metniyle tutarlı biçimde
        // tam gösterilir (maskeleme kaldırıldı: gövde zaten tam adı içeriyor, erişim denetimi
        // tek kullanımlık süreli token'dır).
        var view = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/public/consents/{signToken}", token: null));
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
        var viewBody = await ReadJsonAsync(view);
        Assert.Equal("Onam Hastası", viewBody.GetProperty("patientMaskedName").GetString());
        Assert.Equal((int)ConsentFormStatus.SentBySms, viewBody.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(viewBody.GetProperty("renderedHtml").GetString()));

        // Public imza → Signed.
        var sign = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/public/consents/{signToken}/sign", token: null,
            new { signaturePngBase64 = TinyPngBase64 }));
        Assert.Equal(HttpStatusCode.OK, sign.StatusCode);
        Assert.Equal((int)ConsentFormStatus.Signed,
            (await ReadJsonAsync(sign)).GetProperty("status").GetInt32());

        // Token tek kullanımlık: tekrar GET → 410 Gone.
        var gone = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/public/consents/{signToken}", token: null));
        Assert.Equal(HttpStatusCode.Gone, gone.StatusCode);

        // Klinik tarafı: form Signed + SmsLink kanalı + PDF üretilmiş; PDF ucu akıtıyor.
        var detail = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/consents/{consentId}", token)));
        Assert.Equal((int)ConsentFormStatus.Signed, detail.GetProperty("status").GetInt32());
        Assert.Equal((int)ConsentSignChannel.SmsLink, detail.GetProperty("signChannel").GetInt32());
        Assert.True(detail.GetProperty("pdfFileId").GetInt64() > 0);
        Assert.Equal(64, detail.GetProperty("pdfSha256").GetString()!.Length);

        var pdf = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/consents/{consentId}/pdf", token));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), pdfBytes[..4]);
    }

    [Fact]
    public async Task ConsentExpiredToken_Returns410_AndMarksExpired()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Süresi", "Dolan");
        var (consentId, signToken) = await CreateConsentAndSendSmsAsync(token, patientId);

        // Token süresini geçmişe çek (süper admin bağlamında doğrudan DB).
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            var db = scope.ServiceProvider.GetRequiredService<Dental.Infrastructure.Persistence.AppDbContext>();
            var form = await db.ConsentForms.IgnoreQueryFilters().SingleAsync(f => f.SignToken == signToken);
            form.SignTokenExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        var expired = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/public/consents/{signToken}", token: null));
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);

        // Süre aşımı kalıcı Expired durumu yazar; imza denemesi de 410.
        var detail = await ReadJsonAsync(await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/consents/{consentId}", token)));
        Assert.Equal((int)ConsentFormStatus.Expired, detail.GetProperty("status").GetInt32());

        var sign = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/public/consents/{signToken}/sign", token: null,
            new { signaturePngBase64 = TinyPngBase64 }));
        Assert.Equal(HttpStatusCode.Gone, sign.StatusCode);
    }

    [Fact]
    public async Task ConsentTemplate_ScriptAndEventHandlers_AreSanitized()
    {
        var token = await LoginDemoAsync();
        var create = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/consent-templates", token, new
        {
            name = $"XSS Deneme {Guid.NewGuid():N}",
            bodyHtml = "<p onclick=\"steal()\">Sayın <strong>{{HastaAdi}}</strong></p>" +
                       "<script>alert('xss')</script><iframe src=\"http://evil\"></iframe>" +
                       "<p style=\"color:red\">Onaylıyorum.</p>",
        }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var body = (await ReadJsonAsync(create)).GetProperty("bodyHtml").GetString()!;
        Assert.DoesNotContain("<script", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", body, StringComparison.OrdinalIgnoreCase);
        // İzinli işaretleme ve yer tutucu korunur.
        Assert.Contains("<strong>{{HastaAdi}}</strong>", body);
        Assert.Contains("Onaylıyorum.", body);
    }
}
