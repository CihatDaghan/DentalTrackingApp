using Dental.Application.Abstractions;
using Dental.Application.Consents;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dental.Infrastructure.Consents;

/// <summary>
/// Anonim (token'lı) onam uçları. Denetim (c)-6 düzeltmesi: istekte tenant claim'i yoktur —
/// token→tenant çözümlemesi IgnoreQueryFilters ile yapılır, ardından ITenantScopeFactory ile
/// o kiracı için scope kurulur ve işin tamamı scope içindeki (tenant filtreli) serviste yürür.
/// </summary>
public sealed class PublicConsentService(
    AppDbContext db,
    ITenantScopeFactory scopeFactory) : IPublicConsentService
{
    public async Task<PublicConsentViewDto> GetByTokenAsync(Guid token, CancellationToken ct = default)
    {
        var (formId, tenantId, clinicId) = await ResolveTokenAsync(token, ct);
        using var scope = scopeFactory.CreateScope(tenantId, clinicId);
        var consents = scope.ServiceProvider.GetRequiredService<IConsentService>();
        return await consents.GetPublicViewAsync(formId, ct);
    }

    public async Task<PublicConsentViewDto> SignByTokenAsync(
        Guid token, PublicConsentSignRequest request, string? signerIp, string? signerUserAgent,
        CancellationToken ct = default)
    {
        var (formId, tenantId, clinicId) = await ResolveTokenAsync(token, ct);
        using var scope = scopeFactory.CreateScope(tenantId, clinicId);
        var consents = scope.ServiceProvider.GetRequiredService<IConsentService>();
        return await consents.SignPublicAsync(formId, request, signerIp, signerUserAgent, ct);
    }

    /// <summary>Token filtresiz aranır (yalnız id+tenant projeksiyonu); veri okuma/yazma scope içinde kalır.</summary>
    private async Task<(long FormId, long TenantId, long ClinicId)> ResolveTokenAsync(Guid token, CancellationToken ct)
    {
        var head = await db.ConsentForms.IgnoreQueryFilters().AsNoTracking()
                .Where(f => f.SignToken == token && !f.IsDeleted)
                .Select(f => new { f.Id, f.TenantId, f.ClinicId })
                .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Onam formu bulunamadı.");
        return (head.Id, head.TenantId, head.ClinicId);
    }
}
