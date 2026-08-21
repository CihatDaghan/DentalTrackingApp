using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Messaging;
using Dental.Application.Tenants;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// G aşaması "bitti" kriterleri (mesajlaşma tarafı): İYS/KVKK izin filtresi (ticari izinsiz →
/// Skipped, işlemsel izinsiz → gönderilir), WhatsApp→SMS fallback zinciri, dispatcher'ın
/// geçici/kalıcı hata ayrımı ve 6. denemede Failed, toplu gönderim sayaçları, randevu
/// hatırlatma job'ının mükerrer koruması, WhatsApp webhook imza doğrulaması, kiracı izolasyonu.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class MessagingTests(ApiFixture fx)
{
    /// <summary>FakeSmsProvider'ın "sağlayıcı reddetti" test numarası (E.164 öncesi yerel biçim).</summary>
    private const string RejectPhoneLocal = "05990000000";

    /// <summary>FakeSmsProvider'ın "taşıma hatası" test numarası.</summary>
    private const string TransportFailPhoneLocal = "05980000000";

    private const string NormalPhoneLocal = "05321234567";

    // ---- Yardımcılar ----

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

    private async Task<JsonElement> SendOkAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await fx.Client.SendAsync(Req(method, url, token, body));
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"{method} {url} → {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content).RootElement.Clone();
    }

    private async Task<long> CreatePatientAsync(
        string token, string firstName, string? phone, bool marketingConsent = false, DateOnly? birthDate = null)
    {
        object? consents = marketingConsent
            ? new object[]
            {
                new { consentType = (byte)ConsentType.SmsMarketing, isGranted = true },
                new { consentType = (byte)ConsentType.WhatsApp, isGranted = true },
            }
            : null;

        var dto = await SendOkAsync(HttpMethod.Post, "/api/v1/patients", token, new
        {
            firstName,
            lastName = "Mesaj",
            phone,
            birthDate = birthDate?.ToString("yyyy-MM-dd"),
            consents,
        });
        return dto.GetProperty("id").GetInt64();
    }

    private async Task<JsonElement> GetMessageAsync(string token, long id) =>
        await SendOkAsync(HttpMethod.Get, $"/api/v1/messages/{id}", token);

    private async Task<int> DispatchAsync(string token) =>
        (await SendOkAsync(HttpMethod.Post, "/api/v1/messages/dispatch", token)).GetInt32();

    private async Task<JsonElement[]> ListMessagesAsync(string token, long patientId) =>
        [.. (await SendOkAsync(HttpMethod.Get, $"/api/v1/messages?patientId={patientId}&pageSize=100", token))
            .GetProperty("items").EnumerateArray()];

    // ---- 1) İYS/KVKK izin filtresi ----

    [Fact]
    public async Task CommercialMessage_WithoutConsent_IsSkipped_ButTransactionalIsSent()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "İzinsiz", NormalPhoneLocal);

        // Ticari: izin yok → kayıt oluşur ama Skipped(NoConsent).
        var commercial = await SendOkAsync(HttpMethod.Post, "/api/v1/messages", token, new
        {
            patientId,
            templateKey = MessageTemplateKeys.Bulk,
            channel = (byte)MessageChannel.Sms,
            kind = (byte)MessageKind.Commercial,
        });
        Assert.Equal((int)OutboundMessageState.Skipped, commercial.GetProperty("state").GetInt32());
        Assert.Equal((int)MessageSkipReason.NoConsent, commercial.GetProperty("skipReason").GetInt32());

        // İşlemsel: izin aranmaz → gönderilir.
        var transactional = await SendOkAsync(HttpMethod.Post, "/api/v1/messages", token, new
        {
            patientId,
            templateKey = MessageTemplateKeys.AppointmentReminder,
            channel = (byte)MessageChannel.Sms,
            kind = (byte)MessageKind.Transactional,
        });
        Assert.Equal((int)OutboundMessageState.Pending, transactional.GetProperty("state").GetInt32());

        await DispatchAsync(token);

        var sent = await GetMessageAsync(token, transactional.GetProperty("id").GetInt64());
        Assert.Equal((int)OutboundMessageState.Sent, sent.GetProperty("state").GetInt32());
        Assert.StartsWith("fake-sms-", sent.GetProperty("providerMessageId").GetString());
        Assert.Equal("+905321234567", sent.GetProperty("toAddress").GetString());

        // Ticari kayıt hâlâ Skipped: dispatcher onu hiç denemez.
        var stillSkipped = await GetMessageAsync(token, commercial.GetProperty("id").GetInt64());
        Assert.Equal((int)OutboundMessageState.Skipped, stillSkipped.GetProperty("state").GetInt32());
    }

    [Fact]
    public async Task CommercialMessage_WithConsent_IsQueuedAndSent()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "İzinli", NormalPhoneLocal, marketingConsent: true);

        var dto = await SendOkAsync(HttpMethod.Post, "/api/v1/messages", token, new
        {
            patientId,
            templateKey = MessageTemplateKeys.Bulk,
            channel = (byte)MessageChannel.Sms,
            kind = (byte)MessageKind.Commercial,
        });
        Assert.Equal((int)OutboundMessageState.Pending, dto.GetProperty("state").GetInt32());

        await DispatchAsync(token);
        var sent = await GetMessageAsync(token, dto.GetProperty("id").GetInt64());
        Assert.Equal((int)OutboundMessageState.Sent, sent.GetProperty("state").GetInt32());
    }

    // ---- 2) WhatsApp → SMS fallback zinciri ----

    [Fact]
    public async Task WhatsAppWithoutApprovedTemplate_FallsBackToSms()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Fallback", NormalPhoneLocal);

        var wa = await SendOkAsync(HttpMethod.Post, "/api/v1/messages", token, new
        {
            patientId,
            templateKey = MessageTemplateKeys.AppointmentReminder,
            channel = (byte)MessageChannel.WhatsApp,
            kind = (byte)MessageKind.Transactional,
        });
        var waId = wa.GetProperty("id").GetInt64();

        // 1. tur: onaylı WhatsApp şablonu yok → kalıcı hata + SMS fallback üretilir.
        await DispatchAsync(token);
        var failed = await GetMessageAsync(token, waId);
        Assert.Equal((int)OutboundMessageState.Failed, failed.GetProperty("state").GetInt32());
        Assert.Contains("WhatsApp şablonu", failed.GetProperty("error").GetString());

        var fallback = (await ListMessagesAsync(token, patientId))
            .Single(m => m.TryGetProperty("fallbackOfMessageId", out var f)
                         && f.ValueKind != JsonValueKind.Null && f.GetInt64() == waId);
        Assert.Equal((int)MessageChannel.Sms, fallback.GetProperty("channel").GetInt32());

        // 2. tur: fallback SMS gönderilir.
        await DispatchAsync(token);
        var sent = await GetMessageAsync(token, fallback.GetProperty("id").GetInt64());
        Assert.Equal((int)OutboundMessageState.Sent, sent.GetProperty("state").GetInt32());
        Assert.StartsWith("fake-sms-", sent.GetProperty("providerMessageId").GetString());
    }

    [Fact]
    public async Task WhatsAppWithApprovedTemplate_IsSentWithoutFallback()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "WaOnaylı", NormalPhoneLocal);
        var templateName = $"wa_test_{Guid.NewGuid():N}"[..20];

        await SendOkAsync(HttpMethod.Post, "/api/v1/whatsapp-templates", token, new
        {
            templateName,
            language = "tr",
            category = "utility",
            bodySpec = "Sayin {{1}}, randevunuz {{2}} tarihindedir.",
            paramMapJson = """["hasta_adi","randevu_tarihi"]""",
            metaStatus = (byte)WaTemplateStatus.Approved,
            templateKey = MessageTemplateKeys.Recall,
        });

        var wa = await SendOkAsync(HttpMethod.Post, "/api/v1/messages", token, new
        {
            patientId,
            templateKey = MessageTemplateKeys.Recall,
            channel = (byte)MessageChannel.WhatsApp,
        });

        await DispatchAsync(token);
        var sent = await GetMessageAsync(token, wa.GetProperty("id").GetInt64());
        Assert.Equal((int)OutboundMessageState.Sent, sent.GetProperty("state").GetInt32());
        Assert.StartsWith("fake-wa-", sent.GetProperty("providerMessageId").GetString());

        // Başarılı WhatsApp gönderiminde fallback üretilmez.
        Assert.DoesNotContain(await ListMessagesAsync(token, patientId),
            m => m.GetProperty("fallbackOfMessageId").ValueKind != JsonValueKind.Null);
    }

    // ---- 3) Yeniden deneme / kalıcı hata ----

    [Fact]
    public async Task ProviderRejection_IsPermanent_AndNotRetried()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Reddedilen", RejectPhoneLocal);

        var dto = await SendOkAsync(HttpMethod.Post, "/api/v1/messages", token, new
        {
            patientId,
            templateKey = MessageTemplateKeys.AppointmentReminder,
            channel = (byte)MessageChannel.Sms,
        });

        await DispatchAsync(token);
        var failed = await GetMessageAsync(token, dto.GetProperty("id").GetInt64());
        Assert.Equal((int)OutboundMessageState.Failed, failed.GetProperty("state").GetInt32());
        Assert.Equal(1, failed.GetProperty("attemptCount").GetInt32()); // iş reddi yeniden denenmez
        Assert.Equal(JsonValueKind.Null, failed.GetProperty("nextAttemptAtUtc").ValueKind);
    }

    [Fact]
    public async Task TransientFailure_IsRetriedWithBackoff_AndFailsOnLastAttempt()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Geçici", TransportFailPhoneLocal);

        var dto = await SendOkAsync(HttpMethod.Post, "/api/v1/messages", token, new
        {
            patientId,
            templateKey = MessageTemplateKeys.AppointmentReminder,
            channel = (byte)MessageChannel.Sms,
        });
        var messageId = dto.GetProperty("id").GetInt64();

        await DispatchAsync(token);
        var retrying = await GetMessageAsync(token, messageId);
        Assert.Equal((int)OutboundMessageState.Pending, retrying.GetProperty("state").GetInt32());
        Assert.Equal(1, retrying.GetProperty("attemptCount").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, retrying.GetProperty("nextAttemptAtUtc").ValueKind);

        // Son denemeye kadarki bekleyişi kısaltmak için sayaç doğrudan sınıra çekilir.
        await UsingTenantScopeAsync(async db =>
        {
            var message = await db.OutboundMessages.FirstAsync(m => m.Id == messageId);
            message.AttemptCount = 5;
            message.NextAttemptAtUtc = null;
            await db.SaveChangesAsync();
        });

        await DispatchAsync(token);
        var failed = await GetMessageAsync(token, messageId);
        Assert.Equal((int)OutboundMessageState.Failed, failed.GetProperty("state").GetInt32());
        Assert.Equal(6, failed.GetProperty("attemptCount").GetInt32());
    }

    // ---- 4) Toplu gönderim ----

    [Fact]
    public async Task BulkSend_ReportsTargetedAndSkippedCounts()
    {
        var token = await LoginDemoAsync();
        var withConsent = await CreatePatientAsync(token, "TopluİzinLi", NormalPhoneLocal, marketingConsent: true);
        var withoutConsent = await CreatePatientAsync(token, "TopluİzinSiz", NormalPhoneLocal);
        // İzin kapısı numara kapısından ÖNCE çalışır (hukuki engel önceliklidir);
        // numarasız hastanın InvalidNumber sayılabilmesi için izni olmalıdır.
        var withoutPhone = await CreatePatientAsync(token, "TopluTelsiz", phone: null, marketingConsent: true);

        // Etiket uçları henüz API'de yok; hedef kitle filtresi doğrudan veri üzerinden kurulur.
        var tagId = 0L;
        await UsingTenantScopeAsync(async db =>
        {
            var tag = new Dental.Domain.Entities.PatientTag { Name = $"Toplu-{Guid.NewGuid():N}"[..20] };
            db.PatientTags.Add(tag);
            await db.SaveChangesAsync();
            foreach (var patientId in new[] { withConsent, withoutConsent, withoutPhone })
                db.PatientTagAssignments.Add(new Dental.Domain.Entities.PatientTagAssignment
                {
                    PatientId = patientId,
                    PatientTagId = tag.Id,
                });
            await db.SaveChangesAsync();
            tagId = tag.Id;
        });

        var result = await SendOkAsync(HttpMethod.Post, "/api/v1/messages/bulk", token, new
        {
            templateKey = MessageTemplateKeys.Bulk,
            kind = (byte)MessageKind.Commercial,
            channel = (byte)MessageChannel.Sms,
            filter = new { tagId },
        });

        Assert.Equal(3, result.GetProperty("targeted").GetInt32());
        Assert.Equal(1, result.GetProperty("enqueued").GetInt32());
        Assert.Equal(1, result.GetProperty("skippedNoConsent").GetInt32());
        Assert.Equal(1, result.GetProperty("skippedNoPhone").GetInt32());
    }

    // ---- 5) Randevu hatırlatma otomasyonu ----

    [Fact]
    public async Task AppointmentReminderJob_QueuesOnce_AndMarksReminderState()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Hatırlatma", NormalPhoneLocal);
        var doctorId = await FindDoctorIdAsync(token);
        var clinicId = (await SendOkAsync(HttpMethod.Get, $"/api/v1/patients/{patientId}", token))
            .GetProperty("clinicId").GetInt64();

        var start = DateTime.UtcNow.AddHours(3);
        var appointment = await SendOkAsync(HttpMethod.Post, "/api/v1/appointments", token, new
        {
            clinicId,
            patientId,
            doctorUserId = doctorId,
            startUtc = start,
            endUtc = start.AddMinutes(30),
        });
        var appointmentId = appointment.GetProperty("id").GetInt64();

        var first = await RunAutomationAsync(a => a.QueueAppointmentRemindersAsync());
        Assert.True(first >= 1);

        var messages = await ListMessagesAsync(token, patientId);
        var reminder = Assert.Single(messages, m => m.GetProperty("refType").GetString() == "Appointment"
                                                    && m.GetProperty("refId").GetInt64() == appointmentId);
        Assert.Equal(MessageTemplateKeys.AppointmentReminder, reminder.GetProperty("templateKey").GetString());
        Assert.Contains(start.AddHours(3).ToString("dd.MM.yyyy"), reminder.GetProperty("renderedBody").GetString());

        // Mükerrer koruması: ReminderState=Queued olduğu için ikinci tur aynı randevuyu üretmez.
        await RunAutomationAsync(a => a.QueueAppointmentRemindersAsync());
        var after = await ListMessagesAsync(token, patientId);
        Assert.Single(after, m => m.GetProperty("refType").GetString() == "Appointment"
                                  && m.GetProperty("refId").GetInt64() == appointmentId);

        await UsingTenantScopeAsync(async db =>
        {
            var stored = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appointmentId);
            Assert.Equal(ReminderState.Queued, stored.ReminderState);
        });
    }

    [Fact]
    public async Task BirthdayGreeting_IsCommercial_AndSkipsPatientsWithoutConsent()
    {
        var token = await LoginDemoAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var birthDate = new DateOnly(1990, today.Month, today.Day);

        var granted = await CreatePatientAsync(token, "DoğumİzinLi", NormalPhoneLocal, true, birthDate);
        var denied = await CreatePatientAsync(token, "DoğumİzinSiz", NormalPhoneLocal, false, birthDate);

        // Doğum günü kuralı varsayılan olarak kapalıdır; testte açılır.
        await SendOkAsync(HttpMethod.Post, "/api/v1/automation-rules", token, new
        {
            ruleType = (byte)AutomationRuleType.Birthday,
            isEnabled = true,
            channelPolicy = (byte)ChannelPolicy.SmsOnly,
            templateKey = MessageTemplateKeys.Birthday,
        });

        await RunAutomationAsync(a => a.QueueBirthdayGreetingsAsync());

        var grantedMessage = Assert.Single(await ListMessagesAsync(token, granted));
        Assert.Equal((int)OutboundMessageState.Pending, grantedMessage.GetProperty("state").GetInt32());
        Assert.Equal((int)MessageKind.Commercial, grantedMessage.GetProperty("kind").GetInt32());

        var deniedMessage = Assert.Single(await ListMessagesAsync(token, denied));
        Assert.Equal((int)OutboundMessageState.Skipped, deniedMessage.GetProperty("state").GetInt32());
        Assert.Equal((int)MessageSkipReason.NoConsent, deniedMessage.GetProperty("skipReason").GetInt32());

        // Kuralı tekrar kapat: diğer testlerin sayaçlarını etkilemesin.
        await SendOkAsync(HttpMethod.Post, "/api/v1/automation-rules", token, new
        {
            ruleType = (byte)AutomationRuleType.Birthday,
            isEnabled = false,
            templateKey = MessageTemplateKeys.Birthday,
        });
    }

    // ---- 6) Seed + şablon/kural CRUD ----

    [Fact]
    public async Task Seed_ProvidesSevenTemplatesInTwoLocales_AndFourAutomationRules()
    {
        var token = await LoginDemoAsync();

        var templates = (await SendOkAsync(HttpMethod.Get, "/api/v1/message-templates", token))
            .EnumerateArray().ToList();
        foreach (var key in MessageTemplateKeys.All)
        {
            Assert.Contains(templates, t => t.GetProperty("templateKey").GetString() == key
                                            && t.GetProperty("locale").GetString() == "tr");
            Assert.Contains(templates, t => t.GetProperty("templateKey").GetString() == key
                                            && t.GetProperty("locale").GetString() == "en");
        }

        var rules = (await SendOkAsync(HttpMethod.Get, "/api/v1/automation-rules", token))
            .EnumerateArray().ToList();
        Assert.Equal(4, rules.Count);
        var reminder = rules.Single(r =>
            r.GetProperty("ruleType").GetInt32() == (int)AutomationRuleType.AppointmentReminder);
        Assert.True(reminder.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(24, reminder.GetProperty("offsetHours").GetInt32());
    }

    [Fact]
    public async Task MessageTemplate_DuplicateKeyChannelLocale_IsRejected()
    {
        var token = await LoginDemoAsync();
        var response = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/message-templates", token, new
        {
            templateKey = MessageTemplateKeys.Birthday,
            channel = (byte)MessageChannel.Sms,
            locale = "tr",
            body = "Tekrar eden şablon",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- 7) WhatsApp webhook ----

    [Fact]
    public async Task WhatsAppWebhook_VerifyHandshake_ReturnsChallenge()
    {
        var response = await fx.Client.GetAsync(
            $"/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token={ApiFixture.WhatsAppVerifyToken}&hub.challenge=12345");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("12345", await response.Content.ReadAsStringAsync());

        var wrong = await fx.Client.GetAsync(
            "/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=yanlis&hub.challenge=12345");
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
    }

    [Fact]
    public async Task WhatsAppWebhook_InvalidSignature_IsRejected_AndValidOneMarksDelivered()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Teslim", NormalPhoneLocal);
        var templateName = $"wa_dlv_{Guid.NewGuid():N}"[..20];

        await SendOkAsync(HttpMethod.Post, "/api/v1/whatsapp-templates", token, new
        {
            templateName,
            language = "tr",
            category = "utility",
            bodySpec = "Sayin {{1}}",
            paramMapJson = """["hasta_adi"]""",
            metaStatus = (byte)WaTemplateStatus.Approved,
            templateKey = MessageTemplateKeys.ConsentLink,
        });

        var message = await SendOkAsync(HttpMethod.Post, "/api/v1/messages", token, new
        {
            patientId,
            templateKey = MessageTemplateKeys.ConsentLink,
            channel = (byte)MessageChannel.WhatsApp,
        });
        await DispatchAsync(token);

        var sent = await GetMessageAsync(token, message.GetProperty("id").GetInt64());
        var providerMessageId = sent.GetProperty("providerMessageId").GetString()!;

        var body = $$"""
        {"object":"whatsapp_business_account","entry":[{"id":"1","changes":[{"value":{
          "messaging_product":"whatsapp","metadata":{"phone_number_id":"pnid"},
          "statuses":[{"id":"{{providerMessageId}}","status":"delivered","recipient_id":"905321234567","timestamp":"1760000000"}]
        },"field":"messages"}]}]}
        """;

        // Geçersiz imza: gövde HİÇ işlenmez.
        var bad = await PostWebhookAsync(body, "sha256=" + new string('0', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        var stillSent = await GetMessageAsync(token, message.GetProperty("id").GetInt64());
        Assert.Equal((int)OutboundMessageState.Sent, stillSent.GetProperty("state").GetInt32());

        // Geçerli imza: teslim durumu outbox'a işlenir.
        var ok = await PostWebhookAsync(body, Sign(body));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var delivered = await GetMessageAsync(token, message.GetProperty("id").GetInt64());
        Assert.Equal((int)OutboundMessageState.Delivered, delivered.GetProperty("state").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, delivered.GetProperty("deliveredAtUtc").ValueKind);
    }

    private Task<HttpResponseMessage> PostWebhookAsync(string body, string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/whatsapp")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-Hub-Signature-256", signature);
        return fx.Client.SendAsync(request);
    }

    private static string Sign(string body) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(ApiFixture.WhatsAppAppSecret), Encoding.UTF8.GetBytes(body)));

    // ---- 8) Kiracı izolasyonu ----

    [Fact]
    public async Task Messages_AreIsolatedPerTenant()
    {
        var demoToken = await LoginDemoAsync();
        var demoPatient = await CreatePatientAsync(demoToken, "İzoleDemo", NormalPhoneLocal);
        var demoMessage = await SendOkAsync(HttpMethod.Post, "/api/v1/messages", demoToken, new
        {
            patientId = demoPatient,
            templateKey = MessageTemplateKeys.AppointmentReminder,
            channel = (byte)MessageChannel.Sms,
        });

        var otherEmail = $"msg-{Guid.NewGuid():N}@t.local";
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            await scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>()
                .CreateAsync(new CreateTenantRequest(
                    "Mesaj Kliniği", TenantLegalType.Company, otherEmail, "Test", "Owner", "Test!2026"));
        }

        var otherToken = await LoginAsync(otherEmail, "Test!2026");
        var otherList = await SendOkAsync(HttpMethod.Get, "/api/v1/messages?pageSize=100", otherToken);
        Assert.Empty(otherList.GetProperty("items").EnumerateArray());

        var forbidden = await fx.Client.SendAsync(Req(HttpMethod.Get,
            $"/api/v1/messages/{demoMessage.GetProperty("id").GetInt64()}", otherToken));
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    // ---- Ortak yardımcılar ----

    private async Task<long> FindDoctorIdAsync(string token)
    {
        var doctors = await SendOkAsync(HttpMethod.Get, "/api/v1/doctors", token);
        return doctors.EnumerateArray().First().GetProperty("id").GetInt64();
    }

    /// <summary>Demo kiracı için tenant scope açar (job'ların koştuğu bağlamın aynısı).</summary>
    private async Task UsingTenantScopeAsync(Func<AppDbContext, Task> action)
    {
        var tenantId = await DemoTenantIdAsync();
        using var scope = fx.Services.GetRequiredService<ITenantScopeFactory>().CreateScope(tenantId);
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<int> RunAutomationAsync(Func<IMessageAutomationService, Task<int>> action)
    {
        var tenantId = await DemoTenantIdAsync();
        using var scope = fx.Services.GetRequiredService<ITenantScopeFactory>().CreateScope(tenantId);
        return await action(scope.ServiceProvider.GetRequiredService<IMessageAutomationService>());
    }

    private async Task<long> DemoTenantIdAsync()
    {
        using var scope = fx.Services.CreateScope();
        ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
            .Set(null, null, null, isSuperAdmin: true);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Name == "Demo Diş Kliniği").Select(t => t.Id).FirstAsync();
    }
}
