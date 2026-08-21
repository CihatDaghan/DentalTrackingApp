using Dental.Application.Abstractions;
using Dental.Application.Messaging;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Dental.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Messaging;

/// <summary>Otomasyon kuralları CRUD'u. Kural türü kiracıda tekildir (UQ); yazma settings.update iznine bağlıdır.</summary>
public sealed class AutomationRuleService(AppDbContext db, ITenantContext tenant) : IAutomationRuleService
{
    public async Task<IReadOnlyList<AutomationRuleDto>> ListAsync(CancellationToken ct = default) =>
        await db.AutomationRules.AsNoTracking()
            .OrderBy(r => r.RuleType)
            .Select(r => new AutomationRuleDto(
                r.Id, r.RuleType, r.IsEnabled, r.OffsetHours, r.ChannelPolicy, r.TemplateKey, r.SendAtLocalTime))
            .ToListAsync(ct);

    public async Task<AutomationRuleDto> GetAsync(long id, CancellationToken ct = default) =>
        ToDto(await db.AutomationRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
              ?? throw new KeyNotFoundException("Otomasyon kuralı bulunamadı."));

    public async Task<AutomationRuleDto> UpsertAsync(
        AutomationRuleUpsertRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var rule = await db.AutomationRules.FirstOrDefaultAsync(r => r.RuleType == request.RuleType, ct);
        if (rule is null)
        {
            rule = new AutomationRule
            {
                RuleType = request.RuleType,
                TemplateKey = request.TemplateKey ?? MessagingSeedTemplate.DefaultTemplateKey(request.RuleType),
            };
            db.AutomationRules.Add(rule);
        }

        Apply(rule, request);
        await db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<AutomationRuleDto> UpdateAsync(
        long id, AutomationRuleUpsertRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var rule = await db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Otomasyon kuralı bulunamadı.");

        if (rule.RuleType != request.RuleType &&
            await db.AutomationRules.AnyAsync(r => r.Id != id && r.RuleType == request.RuleType, ct))
            throw new InvalidOperationException("Bu kural türü için kayıt zaten var.");

        rule.RuleType = request.RuleType;
        Apply(rule, request);
        await db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var rule = await db.AutomationRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Otomasyon kuralı bulunamadı.");
        db.AutomationRules.Remove(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> EnsureDefaultsAsync(CancellationToken ct = default)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan varsayılan kural eklenemez.");
        return await MessagingSeedTemplate.ApplyRulesToTenantAsync(db, tenantId, ct);
    }

    private static void Apply(AutomationRule rule, AutomationRuleUpsertRequest request)
    {
        rule.IsEnabled = request.IsEnabled;
        rule.OffsetHours = request.OffsetHours;
        rule.ChannelPolicy = request.ChannelPolicy;
        rule.SendAtLocalTime = request.SendAtLocalTime;
        if (!string.IsNullOrWhiteSpace(request.TemplateKey))
            rule.TemplateKey = request.TemplateKey.Trim().ToLowerInvariant();
    }

    private static void Validate(AutomationRuleUpsertRequest request)
    {
        // Randevu hatırlatması en fazla iki hafta öncesinden anlamlıdır; negatif offset randevu sonrasına düşer.
        if (request.OffsetHours is < 0 or > 336)
            throw new ArgumentOutOfRangeException(nameof(request), "Offset 0-336 saat aralığında olmalıdır.");
    }

    private static AutomationRuleDto ToDto(AutomationRule r) =>
        new(r.Id, r.RuleType, r.IsEnabled, r.OffsetHours, r.ChannelPolicy, r.TemplateKey, r.SendAtLocalTime);
}
