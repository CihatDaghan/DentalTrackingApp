using System.Net;
using System.Net.Http.Json;
using Dental.Application.Abstractions;
using Dental.Application.Tenants;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// Planın A aşaması "bitti" kriterleri:
/// (1) kiracı sızıntı testi — tenant A verisi tenant B sorgusunda asla dönmez,
/// (2) job-modu testi — ITenantScopeFactory ile JWT'siz tenant-scoped sorgu.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class TenantIsolationTests(ApiFixture fx)
{
    private async Task<(long TenantId, long ClinicId)> CreateTenantAsync(string name, string email)
    {
        using var scope = fx.Services.CreateScope();
        // Provisioning süper admin bağlamında koşar (gerçekte AdminTenantsController'dan gelir).
        ((ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>())
            .Set(null, null, null, isSuperAdmin: true);
        var svc = scope.ServiceProvider.GetRequiredService<ITenantProvisioningService>();
        var result = await svc.CreateAsync(new CreateTenantRequest(
            name, TenantLegalType.Company, email, "Test", "Owner", "Test!2026"));
        return (result.TenantId, result.ClinicId);
    }

    [Fact]
    public async Task TenantScopeFactory_QueriesAreIsolatedPerTenant()
    {
        var (tenantA, clinicA) = await CreateTenantAsync("Klinik A", $"a-{Guid.NewGuid():N}@t.local");
        var (tenantB, clinicB) = await CreateTenantAsync("Klinik B", $"b-{Guid.NewGuid():N}@t.local");

        var scopeFactory = fx.Services.GetRequiredService<ITenantScopeFactory>();

        // Job modu: JWT yok, scope factory ile bağlam kurulur.
        using (var scopeA = scopeFactory.CreateScope(tenantA))
        {
            var db = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
            var clinics = await db.Clinics.ToListAsync();
            Assert.Single(clinics);
            Assert.Equal(clinicA, clinics[0].Id);
            Assert.DoesNotContain(clinics, c => c.TenantId == tenantB); // sızıntı yok
        }

        using (var scopeB = scopeFactory.CreateScope(tenantB))
        {
            var db = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
            var clinics = await db.Clinics.ToListAsync();
            Assert.Single(clinics);
            Assert.Equal(clinicB, clinics[0].Id);
        }
    }

    [Fact]
    public async Task CrossTenantWrite_IsRejected()
    {
        var (tenantA, _) = await CreateTenantAsync("Klinik C", $"c-{Guid.NewGuid():N}@t.local");
        var (tenantB, _) = await CreateTenantAsync("Klinik D", $"d-{Guid.NewGuid():N}@t.local");

        var scopeFactory = fx.Services.GetRequiredService<ITenantScopeFactory>();
        using var scope = scopeFactory.CreateScope(tenantA);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Clinics.Add(new Dental.Domain.Entities.Clinic { TenantId = tenantB, Name = "Sızma Denemesi" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SoftDelete_HidesFromQueries_ButKeepsRow()
    {
        var (tenantId, clinicId) = await CreateTenantAsync("Klinik E", $"e-{Guid.NewGuid():N}@t.local");
        var scopeFactory = fx.Services.GetRequiredService<ITenantScopeFactory>();

        using (var scope = scopeFactory.CreateScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clinic = await db.Clinics.SingleAsync(c => c.Id == clinicId);
            db.Clinics.Remove(clinic);
            await db.SaveChangesAsync();
        }

        using (var scope = scopeFactory.CreateScope(tenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Clinics.AnyAsync(c => c.Id == clinicId)); // filtre gizler
            var raw = await db.Clinics.IgnoreQueryFilters().SingleAsync(c => c.Id == clinicId);
            Assert.True(raw.IsDeleted); // satır durur, hard delete olmaz
        }
    }

    [Fact]
    public async Task DemoLogin_Works_And_TenantUserCannotAccessAdminEndpoint()
    {
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "demo@dental.local", password = "Demo!2026" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = body.GetProperty("accessToken").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/tenants");
        request.Headers.Authorization = new("Bearer", token);
        var response = await fx.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WrongPassword_Returns401()
    {
        var login = await fx.Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "demo@dental.local", password = "yanlis-sifre" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
