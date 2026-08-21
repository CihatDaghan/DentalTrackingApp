using System.Security.Cryptography;
using Dental.Application.Abstractions;
using Dental.Application.Consents;
using Dental.Application.Media;
using Dental.Application.Messaging;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dental.Infrastructure.Consents;

/// <summary>
/// Dijital onam: şablon CRUD (sanitize + versiyon), form üretimi (yer tutucu doldurma,
/// RenderedHtml kanıt olarak sabitlenir), tablet imzası, SMS linki ve public imza akışı.
/// PDF üretimi QuestPDF ile; imza görseli ve PDF MediaFile arşivine yazılır.
/// </summary>
public sealed class ConsentService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IMediaService media,
    IMessageOutboxService outbox,
    IOptions<PublicOptions> publicOptions,
    IValidator<ConsentTemplateUpsertRequest> templateValidator,
    IValidator<ConsentCreateRequest> createValidator,
    IValidator<ConsentSignRequest> signValidator,
    IValidator<PublicConsentSignRequest> publicSignValidator) : IConsentService
{
    private static readonly TimeSpan SmsTokenLifetime = TimeSpan.FromHours(72);
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    // ---- Şablonlar ----

    public async Task<IReadOnlyList<ConsentTemplateListItemDto>> ListTemplatesAsync(
        bool includeInactive = false, CancellationToken ct = default)
    {
        var query = db.ConsentTemplates.AsNoTracking();
        if (!includeInactive) query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.Name)
            .Select(t => new ConsentTemplateListItemDto(t.Id, t.Name, t.Locale, t.Version, t.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ConsentTemplateDto> GetTemplateAsync(long id, CancellationToken ct = default)
    {
        var t = await db.ConsentTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Onam şablonu bulunamadı.");
        return ToDto(t);
    }

    public async Task<ConsentTemplateDto> CreateTemplateAsync(
        ConsentTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await templateValidator.ValidateAndThrowAsync(request, ct);
        var template = new ConsentTemplate
        {
            Name = request.Name.Trim(),
            BodyHtml = ConsentHtml.Sanitize(request.BodyHtml), // (c)-7: kayıtta server-side sanitize
            Locale = request.Locale,
            IsActive = request.IsActive,
            Version = 1,
        };
        db.ConsentTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task<ConsentTemplateDto> UpdateTemplateAsync(
        long id, ConsentTemplateUpsertRequest request, CancellationToken ct = default)
    {
        await templateValidator.ValidateAndThrowAsync(request, ct);
        var template = await db.ConsentTemplates.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Onam şablonu bulunamadı.");

        var sanitized = ConsentHtml.Sanitize(request.BodyHtml);
        if (!string.Equals(sanitized, template.BodyHtml, StringComparison.Ordinal))
            template.Version++; // gövde değişti → yeni versiyon; imzalı formlar eski versiyonu snapshot'lar

        template.Name = request.Name.Trim();
        template.BodyHtml = sanitized;
        template.Locale = request.Locale;
        template.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task DeleteTemplateAsync(long id, CancellationToken ct = default)
    {
        var template = await db.ConsentTemplates.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Onam şablonu bulunamadı.");
        db.ConsentTemplates.Remove(template); // soft delete; mevcut formların RenderedHtml'i etkilenmez
        await db.SaveChangesAsync(ct);
    }

    // ---- Formlar ----

    public async Task<ConsentFormDto> CreateAsync(
        long patientId, ConsentCreateRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");
        var template = await db.ConsentTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.IsActive, ct)
            ?? throw new KeyNotFoundException("Onam şablonu bulunamadı.");

        string treatmentName = "-";
        long? doctorUserId = null;
        if (request.TreatmentRecordId is { } trId)
        {
            var treatment = await db.TreatmentRecords.AsNoTracking()
                    .Include(t => t.TreatmentDefinition)
                    .FirstOrDefaultAsync(t => t.Id == trId && t.PatientId == patientId, ct)
                ?? throw new KeyNotFoundException("Tedavi kaydı bulunamadı.");
            treatmentName = treatment.TreatmentDefinition!.Name
                + (treatment.ToothNumber is null ? "" : $" (Diş {treatment.ToothNumber})");
            doctorUserId = treatment.DoctorUserId;
        }

        var doctorName = await ResolveDoctorNameAsync(doctorUserId, ct);
        var clinicName = await GetClinicNameAsync(patient.ClinicId, ct);

        var rendered = template.BodyHtml
            .Replace("{{HastaAdi}}", ConsentHtml.Encode(patient.FullName))
            .Replace("{{HekimAdi}}", ConsentHtml.Encode(doctorName))
            .Replace("{{KlinikAdi}}", ConsentHtml.Encode(clinicName))
            .Replace("{{Tarih}}", ToLocal(clock.UtcNow).ToString("dd.MM.yyyy"))
            .Replace("{{Tedavi}}", ConsentHtml.Encode(treatmentName));

        var form = new ConsentForm
        {
            ClinicId = patient.ClinicId,
            PatientId = patientId,
            TemplateId = template.Id,
            TemplateVersion = template.Version,
            TreatmentRecordId = request.TreatmentRecordId,
            RenderedHtml = ConsentHtml.Sanitize(rendered), // doldurma sonrası ikinci sanitize (savunma katmanı)
            Status = ConsentFormStatus.Draft,
            SignToken = Guid.NewGuid(),
        };
        db.ConsentForms.Add(form);
        await db.SaveChangesAsync(ct);
        return await GetAsync(form.Id, ct);
    }

    public async Task<IReadOnlyList<ConsentFormDto>> ListForPatientAsync(
        long patientId, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new KeyNotFoundException("Hasta bulunamadı.");
        return await Project(db.ConsentForms.AsNoTracking()
                .Where(f => f.PatientId == patientId)
                .OrderByDescending(f => f.CreatedAtUtc).ThenByDescending(f => f.Id))
            .ToListAsync(ct);
    }

    public async Task<ConsentFormDto> GetAsync(long id, CancellationToken ct = default) =>
        await Project(db.ConsentForms.AsNoTracking().Where(f => f.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Onam formu bulunamadı.");

    public async Task<ConsentFormDto> SignTabletAsync(
        long id, ConsentSignRequest request, string? signerIp, string? signerUserAgent, CancellationToken ct = default)
    {
        await signValidator.ValidateAndThrowAsync(request, ct);
        var form = await db.ConsentForms.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new KeyNotFoundException("Onam formu bulunamadı.");
        if (form.Status is not (ConsentFormStatus.Draft or ConsentFormStatus.SentBySms))
            throw new InvalidOperationException("Yalnız taslak veya SMS'te bekleyen onam imzalanabilir.");

        await SignCoreAsync(form, request.SignaturePngBase64, ConsentSignChannel.Tablet, signerIp, signerUserAgent, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ConsentSendSmsResult> SendSmsAsync(long id, CancellationToken ct = default)
    {
        var form = await db.ConsentForms.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new KeyNotFoundException("Onam formu bulunamadı.");
        if (form.Status is not (ConsentFormStatus.Draft or ConsentFormStatus.SentBySms or ConsentFormStatus.Expired))
            throw new InvalidOperationException("İmzalanmış/reddedilmiş onam için SMS gönderilemez.");

        var patient = await db.Patients.AsNoTracking().FirstAsync(p => p.Id == form.PatientId, ct);
        if (string.IsNullOrWhiteSpace(patient.Phone))
            throw new InvalidOperationException("Hastanın kayıtlı telefon numarası yok.");
        var clinicName = await GetClinicNameAsync(form.ClinicId, ct);

        // Token her gönderimde yenilenir; eski link geçersizleşir.
        form.SignToken = Guid.NewGuid();
        form.SignTokenExpiresAtUtc = clock.UtcNow.Add(SmsTokenLifetime);
        form.Status = ConsentFormStatus.SentBySms;
        form.SignChannel = ConsentSignChannel.SmsLink;

        var url = $"{publicOptions.Value.BaseUrl.TrimEnd('/')}/p/consent/{form.SignToken}";
        await db.SaveChangesAsync(ct);

        // G aşaması: gönderim doğrudan ISmsProvider ile değil outbox üzerinden yapılır —
        // izin filtresi, yeniden deneme ve "gönderilenler" kaydı tek yerde toplanır.
        await outbox.EnqueueAsync(new MessageEnqueueRequest(
            MessageTemplateKeys.ConsentLink,
            PatientId: form.PatientId,
            Channel: MessageChannel.Sms,
            Kind: MessageKind.Transactional,
            Params: new Dictionary<string, string>
            {
                [MessagePlaceholders.ConsentLink] = url,
                [MessagePlaceholders.ClinicName] = clinicName,
            },
            RefType: nameof(ConsentForm),
            RefId: form.Id), ct);
        return new ConsentSendSmsResult(form.Id, url, form.SignTokenExpiresAtUtc!.Value, patient.Phone!);
    }

    public async Task<MediaDownload> OpenPdfAsync(long id, CancellationToken ct = default)
    {
        var form = await db.ConsentForms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new KeyNotFoundException("Onam formu bulunamadı.");
        if (form.PdfFileId is not { } pdfFileId)
            throw new KeyNotFoundException("Onam henüz imzalanmadı; PDF üretilmedi.");
        return await media.OpenDownloadAsync(pdfFileId, ct);
    }

    // ---- Public akış (tenant scope içinden; bkz. PublicConsentService) ----

    public async Task<PublicConsentViewDto> GetPublicViewAsync(long formId, CancellationToken ct = default)
    {
        var form = await db.ConsentForms.FirstOrDefaultAsync(f => f.Id == formId, ct)
            ?? throw new KeyNotFoundException("Onam formu bulunamadı.");
        await EnsureTokenUsableAsync(form, ct);
        return await BuildPublicViewAsync(form, ct);
    }

    public async Task<PublicConsentViewDto> SignPublicAsync(
        long formId, PublicConsentSignRequest request, string? signerIp, string? signerUserAgent,
        CancellationToken ct = default)
    {
        await publicSignValidator.ValidateAndThrowAsync(request, ct);
        var form = await db.ConsentForms.FirstOrDefaultAsync(f => f.Id == formId, ct)
            ?? throw new KeyNotFoundException("Onam formu bulunamadı.");
        await EnsureTokenUsableAsync(form, ct);

        if (request.Declined)
        {
            form.Status = ConsentFormStatus.Declined;
            form.SignChannel = ConsentSignChannel.SmsLink;
            form.SignerIp = signerIp;
            form.SignerUserAgent = Truncate(signerUserAgent, 400);
        }
        else
        {
            await SignCoreAsync(form, request.SignaturePngBase64!, ConsentSignChannel.SmsLink,
                signerIp, signerUserAgent, ct);
        }
        await db.SaveChangesAsync(ct);
        return await BuildPublicViewAsync(form, ct);
    }

    // ---- Yardımcılar ----

    /// <summary>Token tek kullanımlık + süreli: imzalı/red/süresi dolmuş → 410 Gone (süre aşımı kalıcı Expired yazar).</summary>
    private async Task EnsureTokenUsableAsync(ConsentForm form, CancellationToken ct)
    {
        if (form.Status is ConsentFormStatus.Signed or ConsentFormStatus.Declined or ConsentFormStatus.Expired)
            throw new ConsentGoneException("Bu onam bağlantısı artık geçerli değil.");
        if (form.SignTokenExpiresAtUtc is { } expires && expires < clock.UtcNow)
        {
            form.Status = ConsentFormStatus.Expired;
            await db.SaveChangesAsync(ct);
            throw new ConsentGoneException("Bu onam bağlantısının süresi dolmuş.");
        }
    }

    private async Task SignCoreAsync(
        ConsentForm form, string signaturePngBase64, ConsentSignChannel channel,
        string? signerIp, string? signerUserAgent, CancellationToken ct)
    {
        var signatureBytes = DecodeSignaturePng(signaturePngBase64);
        var patient = await db.Patients.AsNoTracking().FirstAsync(p => p.Id == form.PatientId, ct);
        var clinicName = await GetClinicNameAsync(form.ClinicId, ct);
        var signedAtUtc = clock.UtcNow;

        var signatureFile = await media.SaveGeneratedAsync(new GeneratedFileRequest(
            form.ClinicId, form.PatientId, MediaCategory.SignatureImage,
            $"onam-{form.Id}-imza.png", "image/png", signatureBytes,
            Description: $"Onam #{form.Id} imza görüntüsü"), ct);

        var pdfBytes = ConsentPdfGenerator.Generate(new ConsentPdfModel(
            clinicName, form.RenderedHtml, patient.FullName, ToLocal(signedAtUtc), signerIp, signatureBytes));
        var pdfFile = await media.SaveGeneratedAsync(new GeneratedFileRequest(
            form.ClinicId, form.PatientId, MediaCategory.ConsentPdf,
            $"onam-{form.Id}.pdf", "application/pdf", pdfBytes,
            Description: $"Onam #{form.Id} imzalı PDF"), ct);

        form.Status = ConsentFormStatus.Signed;
        form.SignChannel = channel;
        form.SignedAtUtc = signedAtUtc;
        form.SignerIp = Truncate(signerIp, 45);
        form.SignerUserAgent = Truncate(signerUserAgent, 400);
        form.SignatureFileId = signatureFile.Id;
        form.PdfFileId = pdfFile.Id;
        form.PdfSha256 = Convert.ToHexStringLower(SHA256.HashData(pdfBytes));
    }

    private static byte[] DecodeSignaturePng(string base64)
    {
        var payload = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64; // data: URI desteği
        byte[] bytes;
        try { bytes = Convert.FromBase64String(payload); }
        catch (FormatException) { throw new InvalidOperationException("İmza görüntüsü geçerli base64 değil."); }
        if (bytes.Length < PngMagic.Length || !bytes.AsSpan(0, PngMagic.Length).SequenceEqual(PngMagic))
            throw new InvalidOperationException("İmza görüntüsü PNG biçiminde olmalıdır.");
        if (bytes.Length > 1024 * 1024)
            throw new InvalidOperationException("İmza görüntüsü 1 MB'ı aşamaz.");
        return bytes;
    }

    private async Task<PublicConsentViewDto> BuildPublicViewAsync(ConsentForm form, CancellationToken ct)
    {
        var patient = await db.Patients.AsNoTracking().FirstAsync(p => p.Id == form.PatientId, ct);
        var clinicName = await GetClinicNameAsync(form.ClinicId, ct);
        var locale = await db.ConsentTemplates.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Id == form.TemplateId).Select(t => t.Locale).FirstOrDefaultAsync(ct) ?? "tr";
        // Onam metni hukuken hastanın tam adını ve tedaviyi içermek zorundadır; başlıkta ad
        // maskelemek yanıltıcı bir koruma hissi veriyordu (inceleme bulgusu). Gerçek erişim
        // denetimi tek kullanımlık + süreli imza token'ıdır; başlık gövdeyle tutarlı gösterilir.
        return new PublicConsentViewDto(
            clinicName, patient.FullName, form.RenderedHtml, form.Status, locale);
    }

    private async Task<string> GetClinicNameAsync(long clinicId, CancellationToken ct) =>
        await db.Clinics.AsNoTracking().Where(c => c.Id == clinicId).Select(c => c.Name).FirstOrDefaultAsync(ct)
            ?? "Klinik";

    private async Task<string> ResolveDoctorNameAsync(long? doctorUserId, CancellationToken ct)
    {
        var userId = doctorUserId ?? tenant.UserId;
        if (userId is null) return "-";
        return await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct) ?? "-";
    }

    private static DateTime ToLocal(DateTime utc)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.AddHours(3); // TR sabit UTC+3
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    private static ConsentTemplateDto ToDto(ConsentTemplate t) =>
        new(t.Id, t.Name, t.BodyHtml, t.Locale, t.Version, t.IsActive);

    private IQueryable<ConsentFormDto> Project(IQueryable<ConsentForm> source) =>
        from f in source
        join t in db.ConsentTemplates.IgnoreQueryFilters() on f.TemplateId equals t.Id
        join p in db.Patients on f.PatientId equals p.Id
        select new ConsentFormDto(
            f.Id, f.PatientId, p.FirstName + " " + p.LastName,
            f.TemplateId, t.Name, f.TemplateVersion, f.TreatmentRecordId,
            f.RenderedHtml, f.Status, f.SignChannel,
            f.SignTokenExpiresAtUtc, f.SignedAtUtc,
            f.SignatureFileId, f.PdfFileId, f.PdfSha256, f.CreatedAtUtc);
}
