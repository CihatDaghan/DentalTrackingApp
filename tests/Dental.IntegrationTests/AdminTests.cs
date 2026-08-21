using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// I aşaması "bitti" kriterleri (süper admin): plan/duyuru CRUD, kiracı yönetimi,
/// entegrasyon sağlığı ve AUDIT'Lİ IMPERSONATION (refresh token üretilmez).
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class AdminTests(ApiFixture fx)
{
    [Fact]
    public async Task Plans_AreSeeded_AndCrudWorks()
    {
        var token = await TestApi.LoginSuperAdminAsync(fx);

        var seeded = await TestApi.GetJsonAsync(fx, "/api/v1/admin/plans", token);
        var codes = seeded.EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();
        Assert.Contains("starter", codes);
        Assert.Contains("pro", codes);
        Assert.Contains("enterprise", codes);

        var code = $"test-{Guid.NewGuid():N}"[..12];
        var create = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/admin/plans", token, new
        {
            code,
            name = "Test Planı",
            maxUsers = 5,
            maxPatients = 500,
            monthlySmsQuota = 100,
            storageGb = 10,
            priceMonthly = 999.90m,
        }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var plan = await create.Content.ReadFromJsonAsync<JsonElement>();
        var planId = plan.GetProperty("id").GetInt64();
        Assert.Equal(code, plan.GetProperty("code").GetString());
        Assert.Equal(0, plan.GetProperty("tenantCount").GetInt32());

        // Aynı kodla ikinci plan reddedilir.
        var duplicate = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/admin/plans", token, new
        {
            code, name = "Kopya", maxUsers = 1, maxPatients = 1, monthlySmsQuota = 1, storageGb = 1, priceMonthly = 1m,
        }));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var update = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, $"/api/v1/admin/plans/{planId}", token, new
        {
            code, name = "Test Planı v2", maxUsers = 9, maxPatients = 900,
            monthlySmsQuota = 300, storageGb = 20, priceMonthly = 1299m, isActive = false,
        }));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test Planı v2", updated.GetProperty("name").GetString());
        Assert.False(updated.GetProperty("isActive").GetBoolean());

        var delete = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Delete, $"/api/v1/admin/plans/{planId}", token));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Plan_WithTenants_CannotBeDeleted()
    {
        var token = await TestApi.LoginSuperAdminAsync(fx);
        var plans = await TestApi.GetJsonAsync(fx, "/api/v1/admin/plans", token);
        var starter = plans.EnumerateArray().Single(p => p.GetProperty("code").GetString() == "starter");
        Assert.True(starter.GetProperty("tenantCount").GetInt32() > 0);

        var delete = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Delete,
            $"/api/v1/admin/plans/{starter.GetProperty("id").GetInt64()}", token));
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
    }

    [Fact]
    public async Task Announcement_IsVisibleToTargetTenantOnly()
    {
        var adminToken = await TestApi.LoginSuperAdminAsync(fx);
        var tenant = await TestApi.CreateTenantAsync(fx, "Duyuru");
        var other = await TestApi.CreateTenantAsync(fx, "DuyuruDisi");

        var title = $"Hedefli duyuru {Guid.NewGuid():N}"[..24];
        var create = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/admin/announcements", adminToken,
            new
            {
                title,
                body = "Yalnız bu kiracıya gösterilir.",
                severity = (byte)AnnouncementSeverity.Warning,
                targetTenantId = tenant.TenantId,
            }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var announcement = await create.Content.ReadFromJsonAsync<JsonElement>();
        var announcementId = announcement.GetProperty("id").GetInt64();
        Assert.Equal(tenant.TenantId, announcement.GetProperty("targetTenantId").GetInt64());

        var targetToken = await TestApi.LoginAsync(fx, tenant);
        var visible = await TestApi.GetJsonAsync(fx, "/api/v1/announcements/active", targetToken);
        Assert.Contains(visible.EnumerateArray(), a => a.GetProperty("id").GetInt64() == announcementId);

        var otherToken = await TestApi.LoginAsync(fx, other);
        var hidden = await TestApi.GetJsonAsync(fx, "/api/v1/announcements/active", otherToken);
        Assert.DoesNotContain(hidden.EnumerateArray(), a => a.GetProperty("id").GetInt64() == announcementId);

        // Pasife alınan duyuru banner'dan düşer.
        var deactivate = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
            $"/api/v1/admin/announcements/{announcementId}", adminToken,
            new { title, body = "Kapatıldı.", severity = (byte)AnnouncementSeverity.Info, isActive = false }));
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var afterDeactivate = await TestApi.GetJsonAsync(fx, "/api/v1/announcements/active", targetToken);
        Assert.DoesNotContain(afterDeactivate.EnumerateArray(), a => a.GetProperty("id").GetInt64() == announcementId);

        var delete = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Delete,
            $"/api/v1/admin/announcements/{announcementId}", adminToken));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task TenantList_ReportsUsageStatistics_AndSupportsSearch()
    {
        var adminToken = await TestApi.LoginSuperAdminAsync(fx);
        var tenant = await TestApi.CreateTenantAsync(fx, "Istatistik");
        var token = await TestApi.LoginAsync(fx, tenant);
        await TestApi.CreatePatientAsync(fx, token, "Sayaç", "Testi");

        var list = await TestApi.GetJsonAsync(fx, "/api/v1/admin/tenants?search=Istatistik", adminToken);
        var row = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(tenant.TenantId, row.GetProperty("id").GetInt64());
        Assert.Equal("starter", row.GetProperty("planCode").GetString());
        var usage = row.GetProperty("usage");
        Assert.Equal(1, usage.GetProperty("userCount").GetInt32());
        Assert.Equal(1, usage.GetProperty("patientCount").GetInt32());
        Assert.False(usage.GetProperty("lastActivityUtc").ValueKind == JsonValueKind.Null);

        var detail = await TestApi.GetJsonAsync(fx, $"/api/v1/admin/tenants/{tenant.TenantId}", adminToken);
        Assert.Single(detail.GetProperty("clinics").EnumerateArray());
        var owner = Assert.Single(detail.GetProperty("owners").EnumerateArray());
        Assert.Equal(tenant.Email, owner.GetProperty("email").GetString());
    }

    [Fact]
    public async Task TenantUpdate_ChangesPlanAndStatus_AndRejectsUnknownPlan()
    {
        var adminToken = await TestApi.LoginSuperAdminAsync(fx);
        var tenant = await TestApi.CreateTenantAsync(fx, "PlanDegis");

        var trialEnd = DateTime.UtcNow.AddDays(45);
        var update = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
            $"/api/v1/admin/tenants/{tenant.TenantId}", adminToken,
            new { planCode = "pro", status = (byte)TenantStatus.Active, trialEndsAtUtc = trialEnd }));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var detail = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pro", detail.GetProperty("planCode").GetString());
        Assert.Equal("Profesyonel", detail.GetProperty("planName").GetString());
        Assert.Equal((int)TenantStatus.Active, detail.GetProperty("status").GetInt32());

        var unknown = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
            $"/api/v1/admin/tenants/{tenant.TenantId}", adminToken, new { planCode = "olmayan-plan" }));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task TenantDelete_RequiresConfirmation_AndIsSoft()
    {
        var adminToken = await TestApi.LoginSuperAdminAsync(fx);
        var tenant = await TestApi.CreateTenantAsync(fx, "Silinen");

        var withoutConfirm = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Delete,
            $"/api/v1/admin/tenants/{tenant.TenantId}", adminToken));
        Assert.Equal(HttpStatusCode.BadRequest, withoutConfirm.StatusCode);

        var deleted = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Delete,
            $"/api/v1/admin/tenants/{tenant.TenantId}?confirm=true", adminToken));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // Varsayılan listede görünmez, includeDeleted ile görünür (satır durur).
        var visible = await TestApi.GetJsonAsync(fx, "/api/v1/admin/tenants?search=Silinen", adminToken);
        Assert.Empty(visible.GetProperty("items").EnumerateArray());
        var all = await TestApi.GetJsonAsync(fx, "/api/v1/admin/tenants?search=Silinen&includeDeleted=true", adminToken);
        var row = Assert.Single(all.GetProperty("items").EnumerateArray());
        Assert.True(row.GetProperty("isDeleted").GetBoolean());
        Assert.Equal((int)TenantStatus.Suspended, row.GetProperty("status").GetInt32());

        // Kullanıcıları pasife alındığı için artık giriş yapılamaz.
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = tenant.Email, password = tenant.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Impersonation_WritesAudit_IssuesNoRefreshToken_AndSeesTargetTenantData()
    {
        var adminToken = await TestApi.LoginSuperAdminAsync(fx);
        var tenant = await TestApi.CreateTenantAsync(fx, "Burunme");
        var tenantToken = await TestApi.LoginAsync(fx, tenant);
        var patientId = await TestApi.CreatePatientAsync(fx, tenantToken, "Bürünme", "Hastası");

        int refreshBefore;
        using (var scope = fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            refreshBefore = await db.RefreshTokens.CountAsync(t => t.UserId == tenant.OwnerUserId);
        }

        var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post,
            $"/api/v1/admin/tenants/{tenant.TenantId}/impersonate", adminToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(tenant.TenantId, result.GetProperty("tenantId").GetInt64());
        Assert.Equal(tenant.OwnerUserId, result.GetProperty("impersonatedUserId").GetInt64());
        Assert.Equal(tenant.Email, result.GetProperty("impersonatedUserEmail").GetString());
        // Kısa ömür: 15 dakika.
        Assert.Equal(900, result.GetProperty("expiresInSeconds").GetInt32());
        // Yanıtta refresh token ALANI YOKTUR.
        Assert.False(result.TryGetProperty("refreshToken", out _));

        long superAdminId;
        using (var scope = fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            superAdminId = await db.Users.IgnoreQueryFilters()
                .Where(u => u.Email == TestApi.SuperAdminEmail).Select(u => u.Id).SingleAsync();
        }

        var accessToken = result.GetProperty("accessToken").GetString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        // impersonated_by = kimliğe bürünen SÜPER ADMİN'in kullanıcı kimliği (izlenebilirlik).
        Assert.Equal(superAdminId.ToString(), jwt.Claims.Single(c => c.Type == "impersonated_by").Value);
        Assert.Equal(tenant.OwnerUserId.ToString(), jwt.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal(tenant.TenantId.ToString(), jwt.Claims.Single(c => c.Type == "tenant_id").Value);
        // Süper admin bayrağı TAŞINMAZ: bürünen oturum yalnız hedef kiracının yetkisine sahiptir.
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "super_admin");
        Assert.True((jwt.ValidTo - DateTime.UtcNow).TotalMinutes <= 15.5);

        // Token hedef kiracının verisini görür...
        var patients = await TestApi.GetJsonAsync(fx, "/api/v1/patients", accessToken);
        Assert.Contains(patients.GetProperty("items").EnumerateArray(),
            p => p.GetProperty("id").GetInt64() == patientId);

        // ...ama süper admin uçlarına erişemez.
        var forbidden = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Get, "/api/v1/admin/tenants", accessToken));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using (var scope = fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // AuditLog: kim, hangi kiracı, ne zaman.
            var audit = await db.AuditLogs.SingleAsync(a =>
                a.Id == result.GetProperty("auditLogId").GetInt64());
            Assert.Equal(AuditActionType.Impersonation, audit.ActionType);
            Assert.Equal(tenant.TenantId, audit.TenantId);
            Assert.Equal(nameof(Dental.Domain.Entities.Tenant), audit.EntityName);
            Assert.Contains(tenant.Email, audit.NewValuesJson);
            Assert.True((DateTime.UtcNow - audit.AtUtc).TotalMinutes < 5);

            // REFRESH TOKEN ÜRETİLMEZ — oturum uzatılamaz.
            var refreshAfter = await db.RefreshTokens.CountAsync(t => t.UserId == tenant.OwnerUserId);
            Assert.Equal(refreshBefore, refreshAfter);
        }
    }

    [Fact]
    public async Task Impersonation_RequiresSuperAdmin()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "BurunmeYetki");
        var token = await TestApi.LoginAsync(fx, tenant);

        var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post,
            $"/api/v1/admin/tenants/{tenant.TenantId}/impersonate", token));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IntegrationHealth_ListsEveryIntegrationKeyPerTenant()
    {
        var adminToken = await TestApi.LoginSuperAdminAsync(fx);
        var tenant = await TestApi.CreateTenantAsync(fx, "Saglik");

        var health = await TestApi.GetJsonAsync(fx,
            $"/api/v1/admin/integration-health?tenantId={tenant.TenantId}", adminToken);
        var row = Assert.Single(health.EnumerateArray());
        Assert.Equal(tenant.TenantId, row.GetProperty("tenantId").GetInt64());

        var keys = row.GetProperty("integrations").EnumerateArray()
            .Select(i => i.GetProperty("integrationKey").GetString()).ToList();
        Assert.Equal(["EInvoice", "Sms", "WhatsApp", "Payment", "Enabiz"], keys);

        // Ayarı olmayan kiracıda entegrasyonlar kapalıdır; e-Nabız modu ve KTS bayrağı raporlanır.
        Assert.All(row.GetProperty("integrations").EnumerateArray(),
            i => Assert.False(i.GetProperty("isEnabled").GetBoolean()));
        Assert.Equal((int)EnabizMode.Disabled, row.GetProperty("enabizMode").GetInt32());
        Assert.False(row.GetProperty("ktsRegistered").GetBoolean());
    }

    [Fact]
    public async Task AdminEndpoints_AreClosedToTenantUsers()
    {
        var token = await TestApi.LoginDemoAsync(fx);

        foreach (var url in new[]
                 {
                     "/api/v1/admin/tenants", "/api/v1/admin/plans",
                     "/api/v1/admin/announcements", "/api/v1/admin/integration-health",
                 })
        {
            var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Get, url, token));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
