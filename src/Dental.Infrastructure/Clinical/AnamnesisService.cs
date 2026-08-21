using Dental.Application.Abstractions;
using Dental.Application.Clinical;
using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Clinical;

/// <summary>
/// Anamnez: şablon CRUD (soru sıralamalı) + versiyonlu hasta yanıtı (her doldurma yeni Response)
/// + kritik cevap özeti (hasta başlığı kırmızı rozet verisi).
/// </summary>
public sealed class AnamnesisService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IValidator<AnamnesisTemplateUpsertRequest> templateValidator,
    IValidator<AnamnesisFillRequest> fillValidator) : IAnamnesisService
{
    // ---- Şablonlar ----

    public async Task<IReadOnlyList<AnamnesisTemplateListItemDto>> ListTemplatesAsync(CancellationToken ct = default) =>
        await db.AnamnesisTemplates.AsNoTracking()
            .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .Select(t => new AnamnesisTemplateListItemDto(
                t.Id, t.Name, t.IsDefault, t.Questions.Count(q => !q.IsDeleted)))
            .ToListAsync(ct);

    public async Task<AnamnesisTemplateDto> GetTemplateAsync(long id, CancellationToken ct = default) =>
        await ProjectTemplate(db.AnamnesisTemplates.AsNoTracking().Where(t => t.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Anamnez şablonu bulunamadı.");

    public async Task<AnamnesisTemplateDto> CreateTemplateAsync(
        AnamnesisTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await templateValidator.ValidateAndThrowAsync(request, ct);

        var template = new AnamnesisTemplate { Name = request.Name.Trim(), IsDefault = request.IsDefault };
        foreach (var q in request.Questions)
            template.Questions.Add(NewQuestion(q));
        db.AnamnesisTemplates.Add(template);

        if (request.IsDefault) await ClearOtherDefaultsAsync(template, ct);
        await db.SaveChangesAsync(ct);
        return await GetTemplateAsync(template.Id, ct);
    }

    public async Task<AnamnesisTemplateDto> UpdateTemplateAsync(
        long id, AnamnesisTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await templateValidator.ValidateAndThrowAsync(request, ct);
        var template = await db.AnamnesisTemplates.Include(t => t.Questions)
                .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Anamnez şablonu bulunamadı.");

        template.Name = request.Name.Trim();
        template.IsDefault = request.IsDefault;

        // Soru mutabakatı: Id eşleşen güncellenir, istekte olmayan soft delete edilir, yeni eklenir.
        // Silinen soruların eski yanıt bağları korunur (yanıt listesi IgnoreQueryFilters ile okur).
        var byId = template.Questions.ToDictionary(q => q.Id);
        var seen = new HashSet<long>();
        foreach (var q in request.Questions)
        {
            if (q.Id is { } qid && byId.TryGetValue(qid, out var existing))
            {
                existing.SortOrder = q.SortOrder;
                existing.QuestionText = q.QuestionText.Trim();
                existing.QuestionTextEn = q.QuestionTextEn?.Trim();
                existing.AnswerType = q.AnswerType;
                existing.OptionsJson = q.OptionsJson;
                existing.IsCritical = q.IsCritical;
                seen.Add(qid);
            }
            else
            {
                template.Questions.Add(NewQuestion(q));
            }
        }
        foreach (var removed in template.Questions.Where(q => q.Id != 0 && !seen.Contains(q.Id)).ToList())
            db.AnamnesisQuestions.Remove(removed); // interceptor soft delete'e çevirir

        if (request.IsDefault) await ClearOtherDefaultsAsync(template, ct);
        await db.SaveChangesAsync(ct);
        return await GetTemplateAsync(id, ct);
    }

    public async Task DeleteTemplateAsync(long id, CancellationToken ct = default)
    {
        var template = await db.AnamnesisTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Anamnez şablonu bulunamadı.");
        if (await db.AnamnesisResponses.AnyAsync(r => r.TemplateId == id, ct))
            throw new InvalidOperationException("Doldurulmuş yanıtı olan şablon silinemez (pasife alınabilir).");
        db.AnamnesisTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
    }

    // ---- Hasta yanıtları ----

    public async Task<AnamnesisResponseDto> FillAsync(
        long patientId, AnamnesisFillRequest request, CancellationToken ct = default)
    {
        await fillValidator.ValidateAndThrowAsync(request, ct);
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");

        var questionIds = await db.AnamnesisQuestions
            .Where(q => q.TemplateId == request.TemplateId)
            .Select(q => q.Id)
            .ToHashSetAsync(ct);
        if (questionIds.Count == 0)
            throw new KeyNotFoundException("Anamnez şablonu bulunamadı.");
        var unknown = request.Answers.Select(a => a.QuestionId).FirstOrDefault(qid => !questionIds.Contains(qid));
        if (unknown != 0)
            throw new InvalidOperationException($"Soru şablona ait değil: {unknown}.");

        var response = new AnamnesisResponse
        {
            PatientId = patientId,
            TemplateId = request.TemplateId,
            FilledByUserId = tenant.UserId
                ?? throw new InvalidOperationException("Anamnez doldurmak için oturum gereklidir."),
            FilledAtUtc = clock.UtcNow,
        };
        foreach (var a in request.Answers)
        {
            response.Answers.Add(new AnamnesisAnswer
            {
                QuestionId = a.QuestionId,
                BoolValue = a.BoolValue,
                TextValue = string.IsNullOrWhiteSpace(a.TextValue) ? null : a.TextValue.Trim(),
            });
        }
        db.AnamnesisResponses.Add(response);
        await db.SaveChangesAsync(ct);

        return (await ListResponsesAsync(patientId, ct)).First(r => r.Id == response.Id);
    }

    public async Task<IReadOnlyList<AnamnesisResponseDto>> ListResponsesAsync(
        long patientId, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");
        var tenantId = tenant.TenantId!.Value;

        var responses = await (
                from r in db.AnamnesisResponses.AsNoTracking()
                where r.PatientId == patientId
                join t in db.AnamnesisTemplates.IgnoreQueryFilters().AsNoTracking()
                    on r.TemplateId equals t.Id
                join u in db.Users on r.FilledByUserId equals u.Id into uj
                from u in uj.DefaultIfEmpty()
                orderby r.FilledAtUtc descending, r.Id descending
                select new
                {
                    r.Id, r.TemplateId, TemplateName = t.Name, r.FilledByUserId,
                    FilledByName = u != null ? u.FirstName + " " + u.LastName : "-",
                    r.FilledAtUtc,
                })
            .ToListAsync(ct);
        if (responses.Count == 0) return [];

        var responseIds = responses.Select(r => r.Id).ToList();
        // Soru metni soft-delete edilmiş sorularda da gösterilmeli (tarihsel kanıt) → IgnoreQueryFilters + elle tenant süzgeci.
        var answers = await (
                from a in db.AnamnesisAnswers.AsNoTracking()
                where responseIds.Contains(a.ResponseId)
                join q in db.AnamnesisQuestions.IgnoreQueryFilters().AsNoTracking().Where(q => q.TenantId == tenantId)
                    on a.QuestionId equals q.Id
                orderby q.SortOrder
                select new { a.ResponseId, Dto = new AnamnesisAnswerDto(
                    a.QuestionId, q.QuestionText, q.AnswerType, q.IsCritical, a.BoolValue, a.TextValue) })
            .ToListAsync(ct);
        var answersByResponse = answers.ToLookup(a => a.ResponseId, a => a.Dto);

        return [.. responses.Select(r => new AnamnesisResponseDto(
            r.Id, r.TemplateId, r.TemplateName, r.FilledByUserId, r.FilledByName, r.FilledAtUtc,
            [.. answersByResponse[r.Id]]))];
    }

    public async Task<IReadOnlyList<CriticalFlagDto>> GetCriticalFlagsAsync(
        long patientId, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");
        var tenantId = tenant.TenantId!.Value;

        // Rozet en SON doldurmadan hesaplanır (güncel klinik durum).
        var latestResponseId = await db.AnamnesisResponses.AsNoTracking()
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.FilledAtUtc).ThenByDescending(r => r.Id)
            .Select(r => (long?)r.Id)
            .FirstOrDefaultAsync(ct);
        if (latestResponseId is null) return [];

        return await (
                from a in db.AnamnesisAnswers.AsNoTracking()
                where a.ResponseId == latestResponseId
                join q in db.AnamnesisQuestions.IgnoreQueryFilters().AsNoTracking().Where(q => q.TenantId == tenantId)
                    on a.QuestionId equals q.Id
                where q.IsCritical && (a.BoolValue == true || (a.TextValue != null && a.TextValue != ""))
                orderby q.SortOrder
                select new CriticalFlagDto(a.QuestionId, q.QuestionText, a.BoolValue, a.TextValue))
            .ToListAsync(ct);
    }

    // ---- Yardımcılar ----

    private static AnamnesisQuestion NewQuestion(AnamnesisQuestionUpsert q) => new()
    {
        SortOrder = q.SortOrder,
        QuestionText = q.QuestionText.Trim(),
        QuestionTextEn = q.QuestionTextEn?.Trim(),
        AnswerType = q.AnswerType,
        OptionsJson = q.OptionsJson,
        IsCritical = q.IsCritical,
    };

    private async Task ClearOtherDefaultsAsync(AnamnesisTemplate current, CancellationToken ct)
    {
        var others = await db.AnamnesisTemplates
            .Where(t => t.IsDefault && t.Id != current.Id)
            .ToListAsync(ct);
        foreach (var t in others.Where(t => !ReferenceEquals(t, current)))
            t.IsDefault = false;
    }

    private IQueryable<AnamnesisTemplateDto> ProjectTemplate(IQueryable<AnamnesisTemplate> source) =>
        source.Select(t => new AnamnesisTemplateDto(
            t.Id, t.Name, t.IsDefault,
            t.Questions.Where(q => !q.IsDeleted)
                .OrderBy(q => q.SortOrder)
                .Select(q => new AnamnesisQuestionDto(
                    q.Id, q.SortOrder, q.QuestionText, q.QuestionTextEn, q.AnswerType, q.OptionsJson, q.IsCritical))
                .ToList()));
}
