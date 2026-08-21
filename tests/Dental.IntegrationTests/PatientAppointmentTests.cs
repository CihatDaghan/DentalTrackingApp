using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Patients;
using Dental.Application.Tenants;
using Dental.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// B aşaması "bitti" kriterleri: hasta CRUD + TCKN hash araması, kiracı izolasyonu,
/// randevu çakışma reddi, recall → randevu dönüşümü.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class PatientAppointmentTests(ApiFixture fx)
{
    private const string ValidTckn = "10000000146";

    private async Task<(string Token, long UserId, long ClinicId)> LoginDemoAsync()
    {
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "demo@dental.local", password = "Demo!2026" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return (
            body.GetProperty("accessToken").GetString()!,
            body.GetProperty("user").GetProperty("id").GetInt64(),
            body.GetProperty("clinics")[0].GetProperty("id").GetInt64());
    }

    private static HttpRequestMessage Req(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<long> CreatePatientAsync(string token, string firstName, string lastName, string? tckn = null)
    {
        var response = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/patients", token,
            new { firstName, lastName, tckn }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        return dto.GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task PatientCrud_And_TcknHashSearch_Work()
    {
        var (token, _, _) = await LoginDemoAsync();
        var id = await CreatePatientAsync(token, "Ayşe", "Tcknlı", ValidTckn);

        // TCKN araması düz metin kolonu olmadan, hash eşitliğiyle bulmalı.
        var search = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/patients?search={ValidTckn}", token));
        var page = await search.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(id, page.GetProperty("items")[0].GetProperty("id").GetInt64());

        // Get: TCKN şifreden geri çözülüp döner.
        var get = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/patients/{id}", token));
        var dto = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ValidTckn, dto.GetProperty("tckn").GetString());
        Assert.False(string.IsNullOrEmpty(dto.GetProperty("fileNo").GetString()));

        // Aynı TCKN ikinci kez -> anlaşılır hata.
        var dup = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/patients", token,
            new { firstName = "Kopya", lastName = "Kayıt", tckn = ValidTckn }));
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);

        // Geçersiz TCKN -> doğrulama hatası.
        var invalid = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/patients", token,
            new { firstName = "Hatalı", lastName = "Tckn", tckn = "12345678901" }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        // Update + soft delete.
        var update = await fx.Client.SendAsync(Req(HttpMethod.Put, $"/api/v1/patients/{id}", token,
            new { firstName = "Ayşe", lastName = "Güncel", tckn = ValidTckn, phone = "0555 111 22 33" }));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var delete = await fx.Client.SendAsync(Req(HttpMethod.Delete, $"/api/v1/patients/{id}", token));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var afterDelete = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/patients/{id}", token));
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Patients_AreIsolatedPerTenant()
    {
        long tenantA, tenantB;
        using (var scope = fx.Services.CreateScope())
        {
            ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
                .Set(null, null, null, isSuperAdmin: true);
            var provisioning = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
            tenantA = (await provisioning.CreateAsync(new CreateTenantRequest(
                "İzolasyon A", TenantLegalType.Company, $"pa-{Guid.NewGuid():N}@t.local", "A", "Owner", "Test!2026"))).TenantId;
            tenantB = (await provisioning.CreateAsync(new CreateTenantRequest(
                "İzolasyon B", TenantLegalType.Company, $"pb-{Guid.NewGuid():N}@t.local", "B", "Owner", "Test!2026"))).TenantId;
        }

        var scopeFactory = fx.Services.GetRequiredService<ITenantScopeFactory>();
        long patientId;
        using (var scopeA = scopeFactory.CreateScope(tenantA))
        {
            var patients = scopeA.ServiceProvider.GetRequiredService<IPatientService>();
            var db = scopeA.ServiceProvider.GetRequiredService<Dental.Infrastructure.Persistence.AppDbContext>();
            var clinicId = db.Clinics.Single(c => c.TenantId == tenantA).Id;
            var created = await patients.CreateAsync(new PatientUpsertRequest(
                "Gizli", "Hasta", ClinicId: clinicId, Tckn: ValidTckn));
            patientId = created.Id;
        }

        using (var scopeB = scopeFactory.CreateScope(tenantB))
        {
            var patients = scopeB.ServiceProvider.GetRequiredService<IPatientService>();
            // A kiracısının hastası B'de ne id ile ne aramayla görünür.
            await Assert.ThrowsAsync<KeyNotFoundException>(() => patients.GetAsync(patientId));
            var list = await patients.ListAsync(new PatientListQuery(Search: ValidTckn));
            Assert.Equal(0, list.TotalCount);
        }
    }

    [Fact]
    public async Task OverlappingAppointment_IsRejected()
    {
        var (token, userId, clinicId) = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Randevu", "Hastası");

        var start = new DateTime(2027, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var first = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/appointments", token,
            new { clinicId, doctorUserId = userId, patientId, startUtc = start, endUtc = start.AddMinutes(30) }));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstDto = await first.Content.ReadFromJsonAsync<JsonElement>();
        var appointmentId = firstDto.GetProperty("id").GetInt64();

        // Aynı hekim, kesişen aralık -> hata.
        var overlap = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/appointments", token,
            new { clinicId, doctorUserId = userId, patientId, startUtc = start.AddMinutes(15), endUtc = start.AddMinutes(45) }));
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);

        // Bitişik aralık (09:30-10:00) çakışma değildir.
        var adjacent = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/appointments", token,
            new { clinicId, doctorUserId = userId, patientId, startUtc = start.AddMinutes(30), endUtc = start.AddMinutes(60) }));
        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);

        // İptal sonrası aynı aralık yeniden kullanılabilir.
        var cancel = await fx.Client.SendAsync(Req(HttpMethod.Put, $"/api/v1/appointments/{appointmentId}/status", token,
            new { status = (byte)AppointmentStatus.Cancelled, cancelReason = "Test iptali" }));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = await cancel.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)AppointmentStatus.Cancelled, cancelled.GetProperty("status").GetInt32());
        Assert.Equal("Test iptali", cancelled.GetProperty("cancelReason").GetString());

        var reuse = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/appointments", token,
            new { clinicId, doctorUserId = userId, patientId, startUtc = start, endUtc = start.AddMinutes(30) }));
        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);
    }

    [Fact]
    public async Task RecallConvert_CreatesControlAppointment_AndLinksIt()
    {
        var (token, userId, clinicId) = await LoginDemoAsync();
        var patientId = await CreatePatientAsync(token, "Kontrol", "Hastası");

        var create = await fx.Client.SendAsync(Req(HttpMethod.Post, "/api/v1/recalls", token,
            new { patientId, suggestedDate = "2027-06-01", reason = "6 aylık kontrol", doctorUserId = userId }));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var recall = await create.Content.ReadFromJsonAsync<JsonElement>();
        var recallId = recall.GetProperty("id").GetInt64();
        Assert.Equal((int)RecallStatus.Planned, recall.GetProperty("status").GetInt32());

        var start = new DateTime(2027, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var convert = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/recalls/{recallId}/convert", token,
            new { clinicId, doctorUserId = userId, startUtc = start, endUtc = start.AddMinutes(30) }));
        Assert.Equal(HttpStatusCode.OK, convert.StatusCode);
        var converted = await convert.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)RecallStatus.ConvertedToAppointment, converted.GetProperty("status").GetInt32());
        var appointmentId = converted.GetProperty("appointmentId").GetInt64();

        // Randevu gerçekten oluştu ve Type=Control.
        var appointment = await fx.Client.SendAsync(Req(HttpMethod.Get, $"/api/v1/appointments/{appointmentId}", token));
        Assert.Equal(HttpStatusCode.OK, appointment.StatusCode);
        var dto = await appointment.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)AppointmentType.Control, dto.GetProperty("type").GetInt32());
        Assert.Equal(patientId, dto.GetProperty("patientId").GetInt64());

        // İkinci convert reddedilir.
        var again = await fx.Client.SendAsync(Req(HttpMethod.Post, $"/api/v1/recalls/{recallId}/convert", token,
            new { clinicId, doctorUserId = userId, startUtc = start.AddHours(2), endUtc = start.AddHours(2).AddMinutes(30) }));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }
}
