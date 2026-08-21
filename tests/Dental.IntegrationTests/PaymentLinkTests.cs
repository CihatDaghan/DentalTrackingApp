using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Messaging;
using Dental.Application.Tenants;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// G aşaması "bitti" kriterleri (ödeme tarafı): link üretimi + mesaj kuyruğu, public sayfa ve
/// durum yoklaması, callback'te SUNUCUDAN yeniden doğrulama ile tahsilat oluşması ve bakiyeye
/// işlenmesi, İDEMPOTAN callback (aynı ödeme ikinci kez → tek tahsilat), süre aşımı işi,
/// kiracı izolasyonu.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class PaymentLinkTests(ApiFixture fx)
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

    private async Task<JsonElement> SendOkAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await fx.Client.SendAsync(Req(method, url, token, body));
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"{method} {url} → {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content).RootElement.Clone();
    }

    private async Task<long> CreatePatientWithDebtAsync(string token, string firstName, decimal debt)
    {
        var patient = await SendOkAsync(HttpMethod.Post, "/api/v1/patients", token, new
        {
            firstName,
            lastName = "Ödeme",
            phone = "05321234567",
        });
        var patientId = patient.GetProperty("id").GetInt64();

        var catalog = await SendOkAsync(HttpMethod.Get, "/api/v1/treatment-catalog?search=DIS-101", token);
        var definitionId = catalog.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("code").GetString() == "DIS-101").GetProperty("id").GetInt64();

        await SendOkAsync(HttpMethod.Post, $"/api/v1/patients/{patientId}/treatments", token, new
        {
            items = new object[]
            {
                new
                {
                    treatmentDefinitionId = definitionId,
                    toothNumber = "16",
                    status = (byte)TreatmentRecordStatus.Done,
                    price = debt,
                },
            },
        });
        return patientId;
    }

    private async Task<decimal> BalanceAsync(string token, long patientId) =>
        (await SendOkAsync(HttpMethod.Get, $"/api/v1/patients/{patientId}", token))
        .GetProperty("balance").GetDecimal();

    /// <summary>Sağlayıcı callback'i tarayıcı POST'udur ve sonunda yönlendirir; yönlendirme izlenmez.</summary>
    private HttpClient NoRedirectClient() =>
        fx.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private Task<HttpResponseMessage> CallbackAsync(HttpClient client, Guid publicToken) =>
        client.PostAsync($"/api/webhooks/iyzico?intent={publicToken:D}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = "" }));

    // ---- 1) Uçtan uca: link → public sayfa → callback → tahsilat ----

    [Fact]
    public async Task PaymentLink_EndToEnd_CreatesCollection_AndIsIdempotent()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientWithDebtAsync(token, "LinkUçtanUca", 2000m);
        Assert.Equal(2000m, await BalanceAsync(token, patientId));

        var link = await SendOkAsync(HttpMethod.Post, "/api/v1/payment-links", token, new
        {
            patientId,
            amount = 500m,
            description = "Tedavi ön ödemesi",
            channel = (byte)MessageChannel.Sms,
        });
        var intentId = link.GetProperty("id").GetInt64();
        var publicToken = link.GetProperty("publicToken").GetGuid();
        Assert.Equal((int)PaymentIntentStatus.LinkSent, link.GetProperty("status").GetInt32());
        Assert.Contains("fake-payment", link.GetProperty("linkUrl").GetString());

        // Link hastaya mesaj olarak kuyruğa girer ve gövdesi public sayfamızı taşır.
        var messageId = link.GetProperty("messageId").GetInt64();
        var message = await SendOkAsync(HttpMethod.Get, $"/api/v1/messages/{messageId}", token);
        Assert.Equal(MessageTemplateKeys.PaymentLink, message.GetProperty("templateKey").GetString());
        Assert.Equal("PaymentIntent", message.GetProperty("refType").GetString());
        Assert.Contains($"/p/payment/{publicToken:D}", message.GetProperty("renderedBody").GetString());

        // Public sayfa: tutar, klinik ve ödeme bağlantısı — auth'suz.
        var view = await fx.Client.GetFromJsonAsync<JsonElement>($"/api/v1/public/payments/{publicToken:D}");
        Assert.Equal(500m, view.GetProperty("amount").GetDecimal());
        Assert.Equal("Demo Diş Kliniği", view.GetProperty("clinicName").GetString());
        Assert.Equal((int)PaymentIntentStatus.LinkSent, view.GetProperty("status").GetInt32());
        Assert.NotNull(view.GetProperty("payUrl").GetString());

        // Callback: sunucudan yeniden doğrulanır, tahsilat oluşur.
        using var client = NoRedirectClient();
        var callback = await CallbackAsync(client, publicToken);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains($"/p/payment/{publicToken:D}", callback.Headers.Location!.ToString());

        var paid = await SendOkAsync(HttpMethod.Get, $"/api/v1/payment-links/{intentId}", token);
        Assert.Equal((int)PaymentIntentStatus.Paid, paid.GetProperty("status").GetInt32());
        Assert.Equal(500m, paid.GetProperty("paidAmount").GetDecimal());
        Assert.NotEqual(JsonValueKind.Null, paid.GetProperty("paymentId").ValueKind);
        var paymentId = paid.GetProperty("paymentId").GetInt64();

        // Tahsilat online link yöntemiyle kaydedilir ve bakiyeye işlenir.
        var payment = await SendOkAsync(HttpMethod.Get, $"/api/v1/payments/{paymentId}", token);
        Assert.Equal((int)PaymentMethod.OnlineLink, payment.GetProperty("method").GetInt32());
        Assert.Equal(1500m, await BalanceAsync(token, patientId));

        // İdempotanlık: aynı callback ikinci kez → yeni tahsilat YOK, bakiye değişmez.
        var second = await CallbackAsync(client, publicToken);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);

        var afterSecond = await SendOkAsync(HttpMethod.Get, $"/api/v1/payment-links/{intentId}", token);
        Assert.Equal(paymentId, afterSecond.GetProperty("paymentId").GetInt64());
        Assert.Equal(1500m, await BalanceAsync(token, patientId));

        var payments = await SendOkAsync(HttpMethod.Get, $"/api/v1/payments?patientId={patientId}", token);
        Assert.Single(payments.GetProperty("items").EnumerateArray());

        // Public durum ucu (sayfa poll'u) ödemeyi görür.
        var status = await fx.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/payments/{publicToken:D}/status");
        Assert.Equal((int)PaymentIntentStatus.Paid, status.GetProperty("status").GetInt32());
        Assert.Equal(500m, status.GetProperty("paidAmount").GetDecimal());
    }

    // ---- 2) İdempotanlık: aynı sağlayıcı ödeme kimliği tek tahsilat üretir ----

    [Fact]
    public async Task DuplicateProviderPaymentId_ProducesSingleCollection()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientWithDebtAsync(token, "İdempotan", 1000m);

        var link = await SendOkAsync(HttpMethod.Post, "/api/v1/payment-links", token,
            new { patientId, amount = 250m, channel = (byte)MessageChannel.Sms });
        var intentId = link.GetProperty("id").GetInt64();
        var publicToken = link.GetProperty("publicToken").GetGuid();

        using var client = NoRedirectClient();
        await CallbackAsync(client, publicToken);

        var providerPaymentId = (await SendOkAsync(HttpMethod.Get, $"/api/v1/payment-links/{intentId}", token))
            .GetProperty("providerPaymentId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(providerPaymentId));

        // Aynı sağlayıcı ödemesini taşıyan İKİNCİ bir niyet: unique filtered index tek kayda izin verir.
        var duplicateRejected = false;
        await UsingTenantScopeAsync(async db =>
        {
            var original = await db.PaymentIntents.AsNoTracking().FirstAsync(i => i.Id == intentId);
            db.PaymentIntents.Add(new Dental.Domain.Entities.PaymentIntent
            {
                PatientId = original.PatientId,
                ClinicId = original.ClinicId,
                Amount = original.Amount,
                PublicToken = Guid.NewGuid(),
                ProviderPaymentId = providerPaymentId,
                Status = PaymentIntentStatus.Paid,
            });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { duplicateRejected = true; }
        });
        Assert.True(duplicateRejected, "ProviderPaymentId filtered UNIQUE indeksi mükerrer kaydı engellemeli.");

        var payments = await SendOkAsync(HttpMethod.Get, $"/api/v1/payments?patientId={patientId}", token);
        Assert.Single(payments.GetProperty("items").EnumerateArray());
    }

    // ---- 3) Süre aşımı ----

    [Fact]
    public async Task ExpiredPaymentLink_IsClosed_AndPublicPageHidesPayUrl()
    {
        var token = await LoginDemoAsync();
        var patientId = await CreatePatientWithDebtAsync(token, "SüresiDolan", 300m);

        var link = await SendOkAsync(HttpMethod.Post, "/api/v1/payment-links", token,
            new { patientId, amount = 100m, channel = (byte)MessageChannel.Sms });
        var intentId = link.GetProperty("id").GetInt64();
        var publicToken = link.GetProperty("publicToken").GetGuid();

        await UsingTenantScopeAsync(async db =>
        {
            var intent = await db.PaymentIntents.FirstAsync(i => i.Id == intentId);
            intent.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        var expired = await RunExpiryAsync();
        Assert.True(expired >= 1);

        var dto = await SendOkAsync(HttpMethod.Get, $"/api/v1/payment-links/{intentId}", token);
        Assert.Equal((int)PaymentIntentStatus.Expired, dto.GetProperty("status").GetInt32());

        var view = await fx.Client.GetFromJsonAsync<JsonElement>($"/api/v1/public/payments/{publicToken:D}");
        Assert.Equal((int)PaymentIntentStatus.Expired, view.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("payUrl").ValueKind);
    }

    // ---- 4) Bilinmeyen token ve kiracı izolasyonu ----

    [Fact]
    public async Task UnknownPublicToken_Returns404()
    {
        var response = await fx.Client.GetAsync($"/api/v1/public/payments/{Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PaymentLinks_AreIsolatedPerTenant()
    {
        var demoToken = await LoginDemoAsync();
        var patientId = await CreatePatientWithDebtAsync(demoToken, "İzoleÖdeme", 400m);
        var link = await SendOkAsync(HttpMethod.Post, "/api/v1/payment-links", demoToken,
            new { patientId, amount = 120m, channel = (byte)MessageChannel.Sms });
        var intentId = link.GetProperty("id").GetInt64();

        var otherEmail = $"pay-{Guid.NewGuid():N}@t.local";
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            await scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>()
                .CreateAsync(new CreateTenantRequest(
                    "Ödeme Kliniği", TenantLegalType.Company, otherEmail, "Test", "Owner", "Test!2026"));
        }

        var otherToken = await LoginAsync(otherEmail, "Test!2026");
        var otherList = await SendOkAsync(HttpMethod.Get, "/api/v1/payment-links", otherToken);
        Assert.Empty(otherList.EnumerateArray());

        var forbidden = await fx.Client.SendAsync(
            Req(HttpMethod.Get, $"/api/v1/payment-links/{intentId}", otherToken));
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    // ---- Ortak yardımcılar ----

    private async Task UsingTenantScopeAsync(Func<AppDbContext, Task> action)
    {
        var tenantId = await DemoTenantIdAsync();
        using var scope = fx.Services.GetRequiredService<ITenantScopeFactory>().CreateScope(tenantId);
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<int> RunExpiryAsync()
    {
        var tenantId = await DemoTenantIdAsync();
        using var scope = fx.Services.GetRequiredService<ITenantScopeFactory>().CreateScope(tenantId);
        return await scope.ServiceProvider
            .GetRequiredService<Dental.Application.Payments.IPaymentLinkService>().ExpireStaleAsync();
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
