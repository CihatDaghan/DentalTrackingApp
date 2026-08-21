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
/// I aşaması "bitti" kriterleri (ayarlar): klinik künyesi, personel yaşam döngüsü
/// (son Owner koruması), yetki matrisi (yeni token'da izinler değişir) ve
/// entegrasyon sırlarının MASKELİ dönmesi + maskeli yazımda korunması.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class SettingsTests(ApiFixture fx)
{
    // ---- Klinik künyesi ----

    [Fact]
    public async Task ClinicSettings_RoundTrip_AndFeedsInvoiceDecisionEngine()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Kunye");
        var token = await TestApi.LoginAsync(fx, tenant);

        var initial = await TestApi.GetJsonAsync(fx, "/api/v1/settings/clinic", token);
        Assert.Equal(tenant.TenantId, initial.GetProperty("tenantId").GetInt64());
        Assert.Equal(tenant.ClinicId, initial.GetProperty("clinicId").GetInt64());
        Assert.Equal("starter", initial.GetProperty("planCode").GetString());

        var update = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, "/api/v1/settings/clinic", token, new
        {
            tenantName = "Dt. Test Muayenehanesi",
            legalType = (byte)TenantLegalType.SoleProprietor,
            clinicName = "Test Kliniği",
            taxNumber = "12345678901",
            taxOffice = "Kadıköy",
            hasHealthTourismAuthorization = true,
            address = "Bağdat Cad. No:1",
            city = "İstanbul",
            district = "Kadıköy",
            phone = "+902161112233",
            email = "info@test.local",
            ckysCode = "998877",
        }));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var saved = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)TenantLegalType.SoleProprietor, saved.GetProperty("legalType").GetInt32());
        Assert.Equal("12345678901", saved.GetProperty("taxNumber").GetString());
        Assert.True(saved.GetProperty("hasHealthTourismAuthorization").GetBoolean());
        Assert.Equal("998877", saved.GetProperty("ckysCode").GetString());

        // LegalType e-belge karar motorunu besler: veri gerçekten Tenant satırına yazılmalı.
        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenant.TenantId);
        Assert.Equal(TenantLegalType.SoleProprietor, row.LegalType);
        Assert.True(row.HasHealthTourismAuthorization);
    }

    [Fact]
    public async Task ClinicSettings_RejectInconsistentTaxNumber()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "KunyeHata");
        var token = await TestApi.LoginAsync(fx, tenant);

        // Şirket türünde 11 haneli (TCKN) numara reddedilir.
        var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, "/api/v1/settings/clinic", token, new
        {
            tenantName = "Şirket A.Ş.",
            legalType = (byte)TenantLegalType.Company,
            clinicName = "Şirket Kliniği",
            taxNumber = "12345678901",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClinicWorkingHours_AreSavedPerDay()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Saatler");
        var token = await TestApi.LoginAsync(fx, tenant);

        var save = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, "/api/v1/settings/working-hours", token, new
        {
            clinicId = tenant.ClinicId,
            items = new object[]
            {
                new { dayOfWeek = (int)DayOfWeek.Monday, openTime = "09:00:00", closeTime = "18:00:00", isClosed = false },
                new { dayOfWeek = (int)DayOfWeek.Sunday, openTime = (string?)null, closeTime = (string?)null, isClosed = true },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var hours = await save.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, hours.GetArrayLength());

        var monday = hours.EnumerateArray().Single(h => h.GetProperty("dayOfWeek").GetInt32() == (int)DayOfWeek.Monday);
        Assert.Equal("09:00:00", monday.GetProperty("openTime").GetString());
        Assert.False(monday.GetProperty("isClosed").GetBoolean());

        // Kapanış açılıştan önce olamaz.
        var invalid = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, "/api/v1/settings/working-hours", token, new
        {
            clinicId = tenant.ClinicId,
            items = new object[]
            {
                new { dayOfWeek = (int)DayOfWeek.Tuesday, openTime = "18:00:00", closeTime = "09:00:00", isClosed = false },
            },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    // ---- Personel ----

    [Fact]
    public async Task StaffInvite_CreatesUserWithTemporaryPassword_AndRolesApply()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Personel");
        var token = await TestApi.LoginAsync(fx, tenant);

        var roles = await TestApi.GetJsonAsync(fx, "/api/v1/settings/roles", token);
        var dentistRoleId = roles.EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == "Dentist").GetProperty("id").GetInt64();
        var secretaryRoleId = roles.EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == "Secretary").GetProperty("id").GetInt64();

        var email = $"hekim-{Guid.NewGuid():N}@t.local";
        var invite = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/settings/staff", token, new
        {
            email,
            firstName = "Yeni",
            lastName = "Hekim",
            userType = (byte)UserType.Dentist,
            roleIds = new[] { dentistRoleId },
            color = "#f59e0b",
            branch = "Endodonti",
            diplomaNo = "D-9988",
        }));
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var result = await invite.Content.ReadFromJsonAsync<JsonElement>();

        var user = result.GetProperty("user");
        var userId = user.GetProperty("id").GetInt64();
        Assert.True(user.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal("#f59e0b", user.GetProperty("color").GetString());
        Assert.Equal("Endodonti", user.GetProperty("branch").GetString());
        Assert.Equal("D-9988", user.GetProperty("diplomaNo").GetString());
        Assert.Single(user.GetProperty("roles").EnumerateArray());

        // Geçici şifre yalnız bu yanıtta döner ve gerçekten çalışır.
        var temporaryPassword = result.GetProperty("temporaryPassword").GetString()!;
        Assert.True(temporaryPassword.Length >= 8);
        var newUserToken = await TestApi.LoginAsync(fx, email, temporaryPassword);
        Assert.False(string.IsNullOrWhiteSpace(newUserToken));

        // Aynı e-posta ikinci kez davet edilemez.
        var duplicate = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/settings/staff", token, new
        {
            email, firstName = "Kopya", lastName = "Kullanıcı",
            userType = (byte)UserType.Secretary, roleIds = new[] { secretaryRoleId },
        }));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        // Rol değişikliği + tip güncellemesi.
        var update = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, $"/api/v1/settings/staff/{userId}", token,
            new
            {
                firstName = "Yeni",
                lastName = "Sekreter",
                userType = (byte)UserType.Secretary,
                roleIds = new[] { secretaryRoleId },
                isActive = true,
            }));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)UserType.Secretary, updated.GetProperty("userType").GetInt32());
        var role = Assert.Single(updated.GetProperty("roles").EnumerateArray());
        Assert.Equal("Secretary", role.GetProperty("name").GetString());

        // Şifre sıfırlama yeni geçici şifre üretir ve eskisi çalışmaz.
        var reset = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Post, $"/api/v1/settings/staff/{userId}/reset-password", token));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var newPassword = (await reset.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("temporaryPassword").GetString()!;
        Assert.NotEqual(temporaryPassword, newPassword);
        var oldLogin = await fx.Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = temporaryPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await TestApi.LoginAsync(fx, email, newPassword)));

        // Pasife alma: satır durur, giriş kapanır.
        var deactivate = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Delete, $"/api/v1/settings/staff/{userId}", token));
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        var afterDeactivate = await fx.Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeactivate.StatusCode);

        var staff = await TestApi.GetJsonAsync(fx, "/api/v1/settings/staff?includeInactive=true", token);
        var row = staff.EnumerateArray().Single(u => u.GetProperty("id").GetInt64() == userId);
        Assert.False(row.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task StaffDeactivate_RejectsSelf_AndLastOwner()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "SonOwner");
        var token = await TestApi.LoginAsync(fx, tenant);

        // Kendi hesabı (ve aynı zamanda tek Owner) pasife alınamaz.
        var self = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Delete, $"/api/v1/settings/staff/{tenant.OwnerUserId}", token));
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

        // İkinci bir Owner davet edilip ilk Owner ONUN tarafından pasife alınabilir...
        var roles = await TestApi.GetJsonAsync(fx, "/api/v1/settings/roles", token);
        var ownerRoleId = roles.EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == "Owner").GetProperty("id").GetInt64();
        var secondEmail = $"owner2-{Guid.NewGuid():N}@t.local";
        var invite = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/settings/staff", token, new
        {
            email = secondEmail, firstName = "İkinci", lastName = "Sahip",
            userType = (byte)UserType.Owner, roleIds = new[] { ownerRoleId },
        }));
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var inviteResult = await invite.Content.ReadFromJsonAsync<JsonElement>();
        var secondId = inviteResult.GetProperty("user").GetProperty("id").GetInt64();
        var secondToken = await TestApi.LoginAsync(fx, secondEmail,
            inviteResult.GetProperty("temporaryPassword").GetString()!);

        var deactivateFirst = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Delete, $"/api/v1/settings/staff/{tenant.OwnerUserId}", secondToken));
        Assert.Equal(HttpStatusCode.NoContent, deactivateFirst.StatusCode);

        // ...ama artık tek kalan Owner (kendisi) pasife alınamaz.
        var lastOwner = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Delete, $"/api/v1/settings/staff/{secondId}", secondToken));
        Assert.Equal(HttpStatusCode.BadRequest, lastOwner.StatusCode);
    }

    // ---- Yetki matrisi ----

    [Fact]
    public async Task RolePermissions_Update_ReflectsInNextToken()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Matris");
        var token = await TestApi.LoginAsync(fx, tenant);

        var roles = await TestApi.GetJsonAsync(fx, "/api/v1/settings/roles", token);
        var secretary = roles.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Secretary");
        var secretaryRoleId = secretary.GetProperty("id").GetInt64();
        var permissions = secretary.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToList();
        Assert.DoesNotContain("report.export", permissions);

        var email = $"sekreter-{Guid.NewGuid():N}@t.local";
        var invite = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/settings/staff", token, new
        {
            email, firstName = "Sekreter", lastName = "Test",
            userType = (byte)UserType.Secretary, roleIds = new[] { secretaryRoleId },
        }));
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var password = (await invite.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("temporaryPassword").GetString()!;

        // İzin eklenmeden ÖNCE dışa aktarım kapalı.
        var before = await TestApi.LoginAsync(fx, email, password);
        var forbidden = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Get, "/api/v1/reports/revenue/export?format=xlsx", before));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var update = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
            $"/api/v1/settings/roles/{secretaryRoleId}/permissions", token,
            new { permissions = permissions.Append("report.export").ToArray() }));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updatedRole = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("report.export",
            updatedRole.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));

        // YENİ token'da izin görünür (mevcut token süresi dolana dek eski izinleri taşır).
        var after = await TestApi.LoginAsync(fx, email, password);
        var allowed = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Get, "/api/v1/reports/revenue/export?format=xlsx", after));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        // İzin değişikliği denetim kaydı bırakır.
        using var scope = fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a =>
            a.TenantId == tenant.TenantId && a.ActionType == AuditActionType.PermissionChange
            && a.EntityId == secretaryRoleId));
    }

    [Fact]
    public async Task OwnerRole_CannotLoseStaffPermission()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Kilitlenme");
        var token = await TestApi.LoginAsync(fx, tenant);

        var roles = await TestApi.GetJsonAsync(fx, "/api/v1/settings/roles", token);
        var owner = roles.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Owner");
        var withoutStaff = owner.GetProperty("permissions").EnumerateArray()
            .Select(p => p.GetString()!).Where(p => p != "settings.staff").ToArray();

        var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
            $"/api/v1/settings/roles/{owner.GetProperty("id").GetInt64()}/permissions", token,
            new { permissions = withoutStaff }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Bilinmeyen izin kodu da reddedilir.
        var unknown = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
            $"/api/v1/settings/roles/{owner.GetProperty("id").GetInt64()}/permissions", token,
            new { permissions = new[] { "settings.staff", "olmayan.izin" } }));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task PermissionCatalog_IsGroupedByModule()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Katalog");
        var token = await TestApi.LoginAsync(fx, tenant);

        var catalog = await TestApi.GetJsonAsync(fx, "/api/v1/settings/permissions", token);
        var byModule = catalog.GetProperty("byModule");
        Assert.Contains("report.view", byModule.GetProperty("report").EnumerateArray().Select(p => p.GetString()));
        Assert.Contains("settings.staff", byModule.GetProperty("settings").EnumerateArray().Select(p => p.GetString()));
    }

    // ---- Entegrasyonlar ----

    [Fact]
    public async Task IntegrationSecrets_AreMaskedInResponses_AndPreservedOnMaskedWrite()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "Entegrasyon");
        var token = await TestApi.LoginAsync(fx, tenant);

        // Ayarı olmayan kiracıda tüm anahtarlar listelenir ama kapalıdır.
        var initial = await TestApi.GetJsonAsync(fx, "/api/v1/settings/integrations", token);
        Assert.Equal(5, initial.GetArrayLength());
        Assert.All(initial.EnumerateArray(), i => Assert.False(i.GetProperty("isEnabled").GetBoolean()));

        const string secret = "cok-gizli-sifre-4321";
        var save = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, "/api/v1/settings/integrations/Sms", token,
            new
            {
                providerKey = "netgsm",
                environment = "Test",
                isEnabled = true,
                settings = new { UserCode = "8503021234", Password = secret, MsgHeader = "KLINIK" },
            }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = await save.Content.ReadFromJsonAsync<JsonElement>();

        // Sır ASLA düz metin dönmez; son 4 hane ile maskelenir.
        var maskedPassword = saved.GetProperty("settings").GetProperty("Password").GetString();
        Assert.NotEqual(secret, maskedPassword);
        Assert.StartsWith("••••", maskedPassword);
        Assert.EndsWith("4321", maskedPassword);
        Assert.Equal("8503021234", saved.GetProperty("settings").GetProperty("UserCode").GetString());
        Assert.Contains("Password", saved.GetProperty("secretFields").EnumerateArray().Select(f => f.GetString()));

        // Maskeli değerle kaydetmek mevcut sırrı KORUR (yazma-tek-yönlü).
        var rewrite = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, "/api/v1/settings/integrations/Sms", token,
            new
            {
                providerKey = "netgsm",
                environment = "Test",
                isEnabled = true,
                settings = new { UserCode = "8503021234", Password = maskedPassword, MsgHeader = "YENIBASLIK" },
            }));
        Assert.Equal(HttpStatusCode.OK, rewrite.StatusCode);
        var rewritten = await rewrite.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("YENIBASLIK", rewritten.GetProperty("settings").GetProperty("MsgHeader").GetString());
        Assert.Equal(maskedPassword, rewritten.GetProperty("settings").GetProperty("Password").GetString());

        // Şifreli JSON'un içinde gerçekten eski sır durmalı.
        using var scope = fx.Services.CreateScope();
        var store = scope.ServiceProvider
            .GetRequiredService<Dental.Application.Abstractions.IIntegrationSettingsStore>();
        var snapshot = await store.GetAsync(tenant.TenantId, "Sms");
        Assert.NotNull(snapshot);
        Assert.Contains(secret, snapshot!.SettingsJson);

        // Bilinmeyen sağlayıcı reddedilir.
        var unknown = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put, "/api/v1/settings/integrations/Sms", token,
            new { providerKey = "olmayan", environment = "Test", isEnabled = true, settings = new { } }));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task IntegrationTest_ReportsResultAndErrorMessage()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "EntegrasyonTest");
        var token = await TestApi.LoginAsync(fx, tenant);

        // Sahte sürücü: kimlik bilgisi gerekmez, test başarılı döner.
        var fake = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Post, "/api/v1/settings/integrations/Sms/test", token));
        Assert.Equal(HttpStatusCode.OK, fake.StatusCode);
        var fakeResult = await fake.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(fakeResult.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(fakeResult.GetProperty("message").GetString()));
        Assert.True(fakeResult.GetProperty("durationMs").GetInt32() >= 0);

        // Kimlik bilgisi olmadan gerçek sağlayıcı seçilirse test eksik alanları bildirir.
        var save = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
            "/api/v1/settings/integrations/Payment", token,
            new { providerKey = "iyzico", environment = "Test", isEnabled = true, settings = new { } }));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var missing = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Post, "/api/v1/settings/integrations/Payment/test", token));
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        var missingResult = await missing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(missingResult.GetProperty("success").GetBoolean());
        Assert.Contains("ApiKey", missingResult.GetProperty("message").GetString());

        var unknown = await fx.Client.SendAsync(
            TestApi.Req(HttpMethod.Post, "/api/v1/settings/integrations/Olmayan/test", token));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task SettingsEndpoints_RequirePermissions()
    {
        var tenant = await TestApi.CreateTenantAsync(fx, "AyarYetki");
        var ownerToken = await TestApi.LoginAsync(fx, tenant);

        var roles = await TestApi.GetJsonAsync(fx, "/api/v1/settings/roles", ownerToken);
        var secretaryRoleId = roles.EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == "Secretary").GetProperty("id").GetInt64();

        var email = $"kisitli-{Guid.NewGuid():N}@t.local";
        var invite = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Post, "/api/v1/settings/staff", ownerToken, new
        {
            email, firstName = "Kısıtlı", lastName = "Kullanıcı",
            userType = (byte)UserType.Secretary, roleIds = new[] { secretaryRoleId },
        }));
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var tempPassword = (await invite.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("temporaryPassword").GetString()!;
        var token = await TestApi.LoginAsync(fx, email, tempPassword);

        // Sekreter rolünde settings.* izinlerinin HİÇBİRİ yoktur (rol matrisi: yalnız *.read + belirli eylemler).
        foreach (var url in new[]
                 {
                     "/api/v1/settings/clinic", "/api/v1/settings/staff",
                     "/api/v1/settings/roles", "/api/v1/settings/integrations",
                 })
        {
            var response = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Get, url, token));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // settings.view izni eklendiğinde klinik künyesi okunabilir hale gelir (yeni token'da).
        var permissions = roles.EnumerateArray()
            .Single(r => r.GetProperty("id").GetInt64() == secretaryRoleId)
            .GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).Append("settings.view").ToArray();
        var grant = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Put,
            $"/api/v1/settings/roles/{secretaryRoleId}/permissions", ownerToken, new { permissions }));
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

        var refreshed = await TestApi.LoginAsync(fx, email, tempPassword);
        var clinic = await fx.Client.SendAsync(TestApi.Req(HttpMethod.Get, "/api/v1/settings/clinic", refreshed));
        Assert.Equal(HttpStatusCode.OK, clinic.StatusCode);
    }
}
