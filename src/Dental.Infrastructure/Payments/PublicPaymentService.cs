using Dental.Application.Abstractions;
using Dental.Application.Payments;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dental.Infrastructure.Payments;

/// <summary>
/// Anonim (token'lı) ödeme uçları. İstekte tenant claim'i yoktur: token → tenant çözümlemesi
/// IgnoreQueryFilters ile YALNIZ id/tenant projeksiyonu okunarak yapılır; veri okuma/yazmanın
/// tamamı ITenantScopeFactory ile kurulan tenant scope'unda kalır (PublicConsentService kalıbı).
/// </summary>
public sealed class PublicPaymentService(
    AppDbContext db,
    ITenantScopeFactory scopeFactory) : IPublicPaymentService
{
    public async Task<PublicPaymentViewDto> GetByTokenAsync(Guid publicToken, CancellationToken ct = default)
    {
        var head = await ResolveAsync(publicToken, null, ct);
        using var scope = scopeFactory.CreateScope(head.TenantId, head.ClinicId);
        return await scope.ServiceProvider.GetRequiredService<IPaymentLinkService>()
            .GetPublicViewAsync(head.Id, ct);
    }

    public async Task<PublicPaymentStatusDto> GetStatusByTokenAsync(
        Guid publicToken, CancellationToken ct = default)
    {
        var head = await ResolveAsync(publicToken, null, ct);
        using var scope = scopeFactory.CreateScope(head.TenantId, head.ClinicId);
        return await scope.ServiceProvider.GetRequiredService<IPaymentLinkService>()
            .GetPublicStatusAsync(head.Id, ct);
    }

    public async Task<PaymentCallbackResult> HandleCallbackAsync(
        Guid? publicToken, string? providerToken, CancellationToken ct = default)
    {
        var head = await ResolveAsync(publicToken, providerToken, ct);
        // Callback anonimdir; tahsilatı "linki oluşturan kullanıcı" adına kaydedebilmek için
        // scope'a o kullanıcı verilir (yoksa kiracının ilk sahibi kullanılır).
        var userId = head.CreatedByUserId ?? await FallbackUserIdAsync(head.TenantId, ct);
        using var scope = scopeFactory.CreateScope(head.TenantId, head.ClinicId, userId);
        return await scope.ServiceProvider.GetRequiredService<IPaymentLinkService>()
            .HandleCallbackAsync(head.Id, ct);
    }

    private async Task<IntentHead> ResolveAsync(Guid? publicToken, string? providerToken, CancellationToken ct)
    {
        var query = db.PaymentIntents.IgnoreQueryFilters().AsNoTracking().Where(i => !i.IsDeleted);
        query = publicToken is { } token
            ? query.Where(i => i.PublicToken == token)
            : !string.IsNullOrWhiteSpace(providerToken)
                ? query.Where(i => i.ProviderToken == providerToken)
                : throw new KeyNotFoundException("Ödeme bağlantısı bulunamadı.");

        return await query
                .Select(i => new IntentHead(i.Id, i.TenantId, i.ClinicId, i.CreatedByUserId))
                .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Ödeme bağlantısı bulunamadı.");
    }

    private async Task<long?> FallbackUserIdAsync(long tenantId, CancellationToken ct) =>
        await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.UserType == UserType.Owner)
            .OrderBy(u => u.Id)
            .Select(u => (long?)u.Id)
            .FirstOrDefaultAsync(ct);

    private sealed record IntentHead(long Id, long TenantId, long ClinicId, long? CreatedByUserId);
}
