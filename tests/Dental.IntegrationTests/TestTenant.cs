using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Tenants;
using Dental.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Dental.IntegrationTests;

/// <summary>Testin kendi kiracısı — bilinen seed verisiyle kesin toplam doğrulaması yapılabilsin diye.</summary>
public sealed record TestTenant(long TenantId, long ClinicId, long OwnerUserId, string Email, string Password);

/// <summary>I aşaması testlerinin ortak HTTP/kiracı yardımcıları.</summary>
public static class TestApi
{
    public const string DemoEmail = "demo@dental.local";
    public const string DemoPassword = "Demo!2026";
    public const string SuperAdminEmail = "admin@dental.local";
    public const string SuperAdminPassword = "Admin!2026";
    public const string TenantPassword = "Test!2026";

    public static async Task<TestTenant> CreateTenantAsync(ApiFixture fx, string name)
    {
        using var scope = fx.Services.CreateScope();
        ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
            .Set(null, null, null, isSuperAdmin: true);
        var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
        var email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@t.local";
        var result = await provisioning.CreateAsync(new CreateTenantRequest(
            name, TenantLegalType.Company, email, "Test", "Owner", TenantPassword));
        return new TestTenant(result.TenantId, result.ClinicId, result.AdminUserId, email, TenantPassword);
    }

    public static async Task<string> LoginAsync(ApiFixture fx, string email, string password)
    {
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    public static Task<string> LoginAsync(ApiFixture fx, TestTenant tenant) =>
        LoginAsync(fx, tenant.Email, tenant.Password);

    public static Task<string> LoginDemoAsync(ApiFixture fx) => LoginAsync(fx, DemoEmail, DemoPassword);

    public static Task<string> LoginSuperAdminAsync(ApiFixture fx) =>
        LoginAsync(fx, SuperAdminEmail, SuperAdminPassword);

    public static HttpRequestMessage Req(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    public static async Task<JsonElement> GetJsonAsync(ApiFixture fx, string url, string token)
    {
        var response = await fx.Client.SendAsync(Req(HttpMethod.Get, url, token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ---- Veri kurulum yardımcıları (HTTP üzerinden, gerçek yoldan) ----

    public static async Task<long> CreatePatientAsync(
        ApiFixture fx, string token, string firstName, string lastName, object? extra = null)
    {
        object body = extra is null
            ? new { firstName, lastName }
            : MergeName(firstName, lastName, extra);
        var response = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/patients", token, body));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    private static object MergeName(string firstName, string lastName, object extra)
    {
        var dictionary = new Dictionary<string, object?>
        {
            ["firstName"] = firstName,
            ["lastName"] = lastName,
        };
        foreach (var property in extra.GetType().GetProperties())
            dictionary[char.ToLowerInvariant(property.Name[0]) + property.Name[1..]] = property.GetValue(extra);
        return dictionary;
    }

    public static async Task<long> FindDefinitionAsync(ApiFixture fx, string token, string code)
    {
        var page = await GetJsonAsync(fx, $"/api/v1/treatment-catalog?search={code}", token);
        return page.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("code").GetString() == code)
            .GetProperty("id").GetInt64();
    }

    public static async Task<long> AddTreatmentAsync(
        ApiFixture fx, string token, long patientId, long definitionId,
        decimal price, decimal discount = 0m,
        TreatmentRecordStatus status = TreatmentRecordStatus.Done, string toothNumber = "36")
    {
        var response = await fx.Client.SendAsync(Req(HttpMethod.Post,
            $"/api/v1/patients/{patientId}/treatments", token, new
            {
                items = new object[]
                {
                    new
                    {
                        treatmentDefinitionId = definitionId, toothNumber,
                        status = (byte)status, price, discountAmount = discount,
                    },
                },
            }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetInt64();
    }

    public static async Task PayAsync(
        ApiFixture fx, string token, long patientId, decimal amount, PaymentMethod method)
    {
        var response = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/payments", token,
            new { amount, method = (byte)method, patientId }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
