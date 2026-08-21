using Dental.Application.Messaging;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Kiracı şablonu: 7 mesaj şablonu (TR + EN) ve 4 otomasyon kuralı.
/// Yeni kiracı açılışında kopyalanır; mevcut kiracılara DbSeeder idempotent uygular
/// (ExpenseCategoryTemplate kalıbı — eksik anahtarlar tamamlanır, mevcutlar ezilmez).
/// </summary>
public static class MessagingSeedTemplate
{
    /// <param name="Body">Yer tutucular: {hasta_adi} {randevu_tarihi} {randevu_saati} {klinik_adi} {bakiye} {odeme_linki} {onam_linki} {hekim_adi}</param>
    private sealed record Seed(string TemplateKey, MessageKind Kind, string Body, string BodyEn);

    private static readonly IReadOnlyList<Seed> Templates =
    [
        new(MessageTemplateKeys.AppointmentReminder, MessageKind.Transactional,
            "Sayin {hasta_adi}, {randevu_tarihi} {randevu_saati} tarihinde {klinik_adi} randevunuz bulunmaktadir. Iyi gunler dileriz.",
            "Dear {hasta_adi}, you have an appointment at {klinik_adi} on {randevu_tarihi} at {randevu_saati}."),
        new(MessageTemplateKeys.Birthday, MessageKind.Commercial,
            "Sayin {hasta_adi}, dogum gununuzu kutlar, saglikli gunler dileriz. {klinik_adi}",
            "Dear {hasta_adi}, happy birthday and best wishes for your health. {klinik_adi}"),
        new(MessageTemplateKeys.PaymentReminder, MessageKind.Transactional,
            "Sayin {hasta_adi}, {klinik_adi} nezdinde {bakiye} TL vadesi gecmis bakiyeniz bulunmaktadir.",
            "Dear {hasta_adi}, you have an overdue balance of {bakiye} TRY at {klinik_adi}."),
        new(MessageTemplateKeys.Recall, MessageKind.Transactional,
            "Sayin {hasta_adi}, kontrol muayeneniz icin {klinik_adi} olarak sizi bekliyoruz. Randevu icin bize ulasabilirsiniz.",
            "Dear {hasta_adi}, it is time for your check-up at {klinik_adi}. Please contact us for an appointment."),
        new(MessageTemplateKeys.ConsentLink, MessageKind.Transactional,
            "{klinik_adi}: Onam formunuzu incelemek ve imzalamak icin: {onam_linki}",
            "{klinik_adi}: Please review and sign your consent form: {onam_linki}"),
        new(MessageTemplateKeys.PaymentLink, MessageKind.Transactional,
            "Sayin {hasta_adi}, {klinik_adi} odemenizi guvenle tamamlamak icin: {odeme_linki}",
            "Dear {hasta_adi}, complete your payment to {klinik_adi} securely: {odeme_linki}"),
        new(MessageTemplateKeys.Bulk, MessageKind.Commercial,
            "Sayin {hasta_adi}, {klinik_adi} olarak sizi bilgilendirmek isteriz.",
            "Dear {hasta_adi}, an update from {klinik_adi}."),
    ];

    private sealed record RuleSeed(AutomationRuleType RuleType, bool IsEnabled, int OffsetHours, TimeOnly? SendAt);

    /// <summary>Varsayılan: yalnız randevu hatırlatma açık (24 sa önce); diğerleri kiracı kararına bırakılır.</summary>
    private static readonly IReadOnlyList<RuleSeed> Rules =
    [
        new(AutomationRuleType.AppointmentReminder, IsEnabled: true, OffsetHours: 24, SendAt: null),
        new(AutomationRuleType.Birthday, IsEnabled: false, OffsetHours: 0, SendAt: new TimeOnly(9, 0)),
        new(AutomationRuleType.PaymentOverdue, IsEnabled: false, OffsetHours: 0, SendAt: new TimeOnly(10, 0)),
        new(AutomationRuleType.Recall, IsEnabled: false, OffsetHours: 72, SendAt: new TimeOnly(10, 0)),
    ];

    public static string DefaultTemplateKey(AutomationRuleType ruleType) => ruleType switch
    {
        AutomationRuleType.AppointmentReminder => MessageTemplateKeys.AppointmentReminder,
        AutomationRuleType.Birthday => MessageTemplateKeys.Birthday,
        AutomationRuleType.PaymentOverdue => MessageTemplateKeys.PaymentReminder,
        _ => MessageTemplateKeys.Recall,
    };

    /// <summary>
    /// Eksik şablon ve kuralları kiracıya ekler (anahtara göre idempotent).
    /// SuperAdmin bağlamında çağrılmalıdır (TenantId elle atanır). Eklenen kayıt sayısını döner.
    /// </summary>
    public static async Task<int> ApplyToTenantAsync(AppDbContext db, long tenantId, CancellationToken ct = default)
        => await ApplyTemplatesToTenantAsync(db, tenantId, ct) + await ApplyRulesToTenantAsync(db, tenantId, ct);

    public static async Task<int> ApplyTemplatesToTenantAsync(
        AppDbContext db, long tenantId, CancellationToken ct = default)
    {
        var existing = await db.MessageTemplates.IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => new { t.TemplateKey, t.Channel, t.Locale })
            .ToListAsync(ct);
        var known = existing
            .Select(t => $"{t.TemplateKey}|{(byte)t.Channel}|{t.Locale}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var count = 0;
        foreach (var seed in Templates)
        {
            count += TryAdd(db, tenantId, known, seed.TemplateKey, "tr", seed.Body, seed.Kind);
            count += TryAdd(db, tenantId, known, seed.TemplateKey, "en", seed.BodyEn, seed.Kind);
        }

        if (count > 0) await db.SaveChangesAsync(ct);
        return count;
    }

    public static async Task<int> ApplyRulesToTenantAsync(
        AppDbContext db, long tenantId, CancellationToken ct = default)
    {
        var existing = await db.AutomationRules.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Select(r => r.RuleType)
            .ToHashSetAsync(ct);

        var count = 0;
        foreach (var rule in Rules.Where(r => !existing.Contains(r.RuleType)))
        {
            db.AutomationRules.Add(new AutomationRule
            {
                TenantId = tenantId,
                RuleType = rule.RuleType,
                IsEnabled = rule.IsEnabled,
                OffsetHours = rule.OffsetHours,
                ChannelPolicy = ChannelPolicy.WhatsAppFirstThenSms,
                TemplateKey = DefaultTemplateKey(rule.RuleType),
                SendAtLocalTime = rule.SendAt,
            });
            count++;
        }

        if (count > 0) await db.SaveChangesAsync(ct);
        return count;
    }

    private static int TryAdd(
        AppDbContext db, long tenantId, HashSet<string> known,
        string templateKey, string locale, string body, MessageKind kind)
    {
        if (!known.Add($"{templateKey}|{(byte)MessageChannel.Sms}|{locale}")) return 0;
        db.MessageTemplates.Add(new MessageTemplate
        {
            TenantId = tenantId,
            TemplateKey = templateKey,
            Channel = MessageChannel.Sms,
            Locale = locale,
            Body = body,
            Kind = kind,
            IsActive = true,
        });
        return 1;
    }
}
