using Dental.Application.Messaging;
using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Messaging;

/// <summary>Mesaj şablonu ve WhatsApp (Meta) şablon kayıtlarının CRUD'u.</summary>
public sealed class MessageTemplateService(
    AppDbContext db,
    IValidator<MessageTemplateUpsertRequest> templateValidator,
    IValidator<WhatsAppTemplateUpsertRequest> waValidator) : IMessageTemplateService
{
    public async Task<IReadOnlyList<MessageTemplateDto>> ListAsync(
        bool includeInactive = false, CancellationToken ct = default)
    {
        var query = db.MessageTemplates.AsNoTracking();
        if (!includeInactive) query = query.Where(t => t.IsActive);
        return await query
            .OrderBy(t => t.TemplateKey).ThenBy(t => t.Channel).ThenBy(t => t.Locale)
            .Select(t => new MessageTemplateDto(
                t.Id, t.TemplateKey, t.Channel, t.Locale, t.Body, t.Kind, t.IsActive))
            .ToListAsync(ct);
    }

    public async Task<MessageTemplateDto> GetAsync(long id, CancellationToken ct = default) =>
        ToDto(await db.MessageTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
              ?? throw new KeyNotFoundException("Mesaj şablonu bulunamadı."));

    public async Task<MessageTemplateDto> CreateAsync(
        MessageTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await templateValidator.ValidateAndThrowAsync(request, ct);
        await EnsureUniqueAsync(request, null, ct);

        var template = new MessageTemplate
        {
            TemplateKey = request.TemplateKey.Trim().ToLowerInvariant(),
            Channel = request.Channel,
            Locale = request.Locale.Trim().ToLowerInvariant(),
            Body = request.Body.Trim(),
            Kind = request.Kind,
            IsActive = request.IsActive,
        };
        db.MessageTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task<MessageTemplateDto> UpdateAsync(
        long id, MessageTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await templateValidator.ValidateAndThrowAsync(request, ct);
        var template = await db.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Mesaj şablonu bulunamadı.");
        await EnsureUniqueAsync(request, id, ct);

        template.TemplateKey = request.TemplateKey.Trim().ToLowerInvariant();
        template.Channel = request.Channel;
        template.Locale = request.Locale.Trim().ToLowerInvariant();
        template.Body = request.Body.Trim();
        template.Kind = request.Kind;
        template.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var template = await db.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Mesaj şablonu bulunamadı.");
        db.MessageTemplates.Remove(template); // soft delete; gönderilmiş mesajların metni etkilenmez
        await db.SaveChangesAsync(ct);
    }

    // ---- WhatsApp şablonları ----

    public async Task<IReadOnlyList<WhatsAppTemplateDto>> ListWhatsAppAsync(CancellationToken ct = default) =>
        await db.WhatsAppTemplates.AsNoTracking()
            .OrderBy(t => t.TemplateName).ThenBy(t => t.Language)
            .Select(t => new WhatsAppTemplateDto(
                t.Id, t.TemplateName, t.Language, t.Category, t.BodySpec,
                t.ParamMapJson, t.MetaStatus, t.MetaUpdatedAtUtc, t.TemplateKey))
            .ToListAsync(ct);

    public async Task<WhatsAppTemplateDto> GetWhatsAppAsync(long id, CancellationToken ct = default) =>
        ToDto(await db.WhatsAppTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
              ?? throw new KeyNotFoundException("WhatsApp şablonu bulunamadı."));

    public async Task<WhatsAppTemplateDto> CreateWhatsAppAsync(
        WhatsAppTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await waValidator.ValidateAndThrowAsync(request, ct);
        var name = request.TemplateName.Trim().ToLowerInvariant();
        var language = request.Language.Trim().ToLowerInvariant();
        if (await db.WhatsAppTemplates.AnyAsync(t => t.TemplateName == name && t.Language == language, ct))
            throw new InvalidOperationException("Bu ad ve dil için WhatsApp şablonu zaten var.");

        var template = new WhatsAppTemplate
        {
            TemplateName = name,
            Language = language,
            Category = request.Category.Trim().ToLowerInvariant(),
            BodySpec = request.BodySpec.Trim(),
            ParamMapJson = request.ParamMapJson,
            MetaStatus = request.MetaStatus,
            TemplateKey = request.TemplateKey?.Trim().ToLowerInvariant(),
        };
        db.WhatsAppTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task<WhatsAppTemplateDto> UpdateWhatsAppAsync(
        long id, WhatsAppTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await waValidator.ValidateAndThrowAsync(request, ct);
        var template = await db.WhatsAppTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("WhatsApp şablonu bulunamadı.");

        var name = request.TemplateName.Trim().ToLowerInvariant();
        var language = request.Language.Trim().ToLowerInvariant();
        if (await db.WhatsAppTemplates.AnyAsync(
                t => t.Id != id && t.TemplateName == name && t.Language == language, ct))
            throw new InvalidOperationException("Bu ad ve dil için WhatsApp şablonu zaten var.");

        // Onay durumu değiştiyse Meta'dan gelen son güncelleme anı damgalanır.
        if (template.MetaStatus != request.MetaStatus) template.MetaUpdatedAtUtc = DateTime.UtcNow;

        template.TemplateName = name;
        template.Language = language;
        template.Category = request.Category.Trim().ToLowerInvariant();
        template.BodySpec = request.BodySpec.Trim();
        template.ParamMapJson = request.ParamMapJson;
        template.MetaStatus = request.MetaStatus;
        template.TemplateKey = request.TemplateKey?.Trim().ToLowerInvariant();
        await db.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task DeleteWhatsAppAsync(long id, CancellationToken ct = default)
    {
        var template = await db.WhatsAppTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("WhatsApp şablonu bulunamadı.");
        db.WhatsAppTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueAsync(MessageTemplateUpsertRequest request, long? excludeId, CancellationToken ct)
    {
        var key = request.TemplateKey.Trim().ToLowerInvariant();
        var locale = request.Locale.Trim().ToLowerInvariant();
        var clash = await db.MessageTemplates.AnyAsync(
            t => t.Id != excludeId && t.TemplateKey == key && t.Channel == request.Channel && t.Locale == locale, ct);
        if (clash)
            throw new InvalidOperationException("Bu şablon anahtarı, kanal ve dil için kayıt zaten var.");
    }

    private static MessageTemplateDto ToDto(MessageTemplate t) =>
        new(t.Id, t.TemplateKey, t.Channel, t.Locale, t.Body, t.Kind, t.IsActive);

    private static WhatsAppTemplateDto ToDto(WhatsAppTemplate t) =>
        new(t.Id, t.TemplateName, t.Language, t.Category, t.BodySpec,
            t.ParamMapJson, t.MetaStatus, t.MetaUpdatedAtUtc, t.TemplateKey);
}
