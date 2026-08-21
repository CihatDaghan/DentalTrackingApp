using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Common;
using Dental.Application.Messaging;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Messaging;

/// <summary>
/// Giden mesaj kuyruğu. Şablon çözümü → yer tutucu doldurma → İYS/KVKK izin kontrolü →
/// numara normalizasyonu → OutboundMessage(Pending). Gerçek gönderim MessageDispatcher'ındır.
/// </summary>
public sealed class MessageOutboxService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    ILogger<MessageOutboxService> logger) : IMessageOutboxService
{
    public async Task<OutboundMessageDto> EnqueueAsync(
        MessageEnqueueRequest request, CancellationToken ct = default)
    {
        var message = await BuildAsync(request, ct);
        db.OutboundMessages.Add(message);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Mesaj kuyruğa alındı. Id={Id} Kanal={Channel} Şablon={Template} Durum={State} Ref={RefType}#{RefId}",
            message.Id, message.Channel, message.TemplateKey, message.State, message.RefType, message.RefId);
        return await GetAsync(message.Id, ct);
    }

    public async Task<BulkMessageResult> EnqueueBulkAsync(
        BulkMessageRequest request, CancellationToken ct = default)
    {
        var patientIds = await ResolveAudienceAsync(request.Filter, ct);

        var created = new List<OutboundMessage>(patientIds.Count);
        var noConsent = 0;
        var noPhone = 0;
        var enqueued = 0;

        foreach (var patientId in patientIds)
        {
            ct.ThrowIfCancellationRequested();
            var message = await BuildAsync(new MessageEnqueueRequest(
                request.TemplateKey,
                PatientId: patientId,
                Channel: request.Channel,
                Kind: request.Kind,
                ScheduledAtUtc: request.ScheduledAtUtc,
                RefType: "Bulk",
                BodyOverride: request.BodyOverride), ct);

            db.OutboundMessages.Add(message);
            created.Add(message);
            switch (message.State)
            {
                case OutboundMessageState.Skipped when message.SkipReason == MessageSkipReason.NoConsent:
                    noConsent++;
                    break;
                case OutboundMessageState.Skipped when message.SkipReason == MessageSkipReason.InvalidNumber:
                    noPhone++;
                    break;
                default:
                    enqueued++;
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
        var ids = created.Select(m => m.Id).ToList();

        logger.LogInformation(
            "Toplu gönderim kuyruğa alındı. Hedef={Targeted} Kuyruk={Enqueued} İzinsiz={NoConsent} Numarasız={NoPhone}",
            patientIds.Count, enqueued, noConsent, noPhone);

        return new BulkMessageResult(patientIds.Count, enqueued, noConsent, noPhone, ids);
    }

    public async Task<PagedResult<OutboundMessageDto>> ListAsync(
        MessageListQuery query, CancellationToken ct = default)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var source = db.OutboundMessages.AsNoTracking();

        if (query.Channel is { } channel) source = source.Where(m => m.Channel == channel);
        if (query.State is { } state) source = source.Where(m => m.State == state);
        if (query.PatientId is { } patientId) source = source.Where(m => m.PatientId == patientId);
        if (query.From is { } from)
            source = source.Where(m => m.CreatedAtUtc >= TrTime.DayRangeUtc(from).StartUtc);
        if (query.To is { } to)
            source = source.Where(m => m.CreatedAtUtc < TrTime.DayRangeUtc(to).EndUtc);

        var total = await source.CountAsync(ct);
        var items = await Project(source.OrderByDescending(m => m.Id).Skip(page.Skip).Take(page.EffectivePageSize))
            .ToListAsync(ct);
        return new PagedResult<OutboundMessageDto>(items, page.Page, page.EffectivePageSize, total);
    }

    public async Task<OutboundMessageDto> GetAsync(long id, CancellationToken ct = default) =>
        await Project(db.OutboundMessages.AsNoTracking().Where(m => m.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Mesaj bulunamadı.");

    // ---- Kuruluş ----

    /// <summary>
    /// Kaydı hazırlar ama DB'ye eklemez (toplu gönderimde tek SaveChanges kullanılabilsin diye).
    /// İzin/numara engelinde kayıt yine üretilir; State=Skipped olur.
    /// </summary>
    private async Task<OutboundMessage> BuildAsync(MessageEnqueueRequest request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan mesaj kuyruğa alınamaz.");

        var patient = request.PatientId is { } pid
            ? await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct)
                ?? throw new KeyNotFoundException("Hasta bulunamadı.")
            : null;

        var locale = request.Locale ?? await DefaultLocaleAsync(tenantId, ct);
        var channel = request.Channel ?? await ResolveChannelAsync(request.TemplateKey, ct);
        var template = await FindTemplateAsync(request.TemplateKey, channel, locale, ct);

        var parameters = await BuildParamsAsync(patient, request.Params, ct);
        var body = request.BodyOverride ?? template?.Body;

        var message = new OutboundMessage
        {
            PatientId = request.PatientId,
            Channel = channel,
            Kind = request.Kind,
            TemplateKey = request.TemplateKey,
            RenderedBody = body is null ? "" : MessageRenderer.Render(body, parameters),
            ParamsJson = parameters.Count == 0 ? null : JsonSerializer.Serialize(parameters),
            ToAddress = "",
            ScheduledAtUtc = request.ScheduledAtUtc ?? clock.UtcNow,
            RefType = request.RefType,
            RefId = request.RefId,
            FallbackOfMessageId = request.FallbackOfMessageId,
            CorrelationId = Guid.NewGuid().ToString("N"),
            State = OutboundMessageState.Pending,
        };

        if (body is null)
        {
            Skip(message, MessageSkipReason.NoTemplate,
                $"'{request.TemplateKey}' için {channel}/{locale} şablonu yok.");
            return message;
        }

        // İYS/KVKK: ticari mesaj gönderim öncesi kanal bazında izin ister; işlemsel mesaj istemez.
        if (request.Kind == MessageKind.Commercial && patient is not null &&
            !await HasConsentAsync(patient.Id, channel, ct))
        {
            message.ToAddress = PhoneNumbers.NormalizeTr(patient.Phone) ?? patient.Phone ?? "";
            Skip(message, MessageSkipReason.NoConsent, "Ticari mesaj için iletişim izni yok.");
            return message;
        }

        var address = ResolveAddress(request.ToAddressOverride, patient, channel);
        if (address is null)
        {
            Skip(message, MessageSkipReason.InvalidNumber, "Geçerli alıcı adresi yok.");
            return message;
        }

        message.ToAddress = address;
        return message;
    }

    private static void Skip(OutboundMessage message, MessageSkipReason reason, string detail)
    {
        message.State = OutboundMessageState.Skipped;
        message.SkipReason = reason;
        message.Error = detail;
    }

    private static string? ResolveAddress(string? overrideValue, Patient? patient, MessageChannel channel)
    {
        if (channel == MessageChannel.Email)
        {
            var email = overrideValue ?? patient?.Email;
            return string.IsNullOrWhiteSpace(email) || !email.Contains('@') ? null : email.Trim();
        }

        return PhoneNumbers.NormalizeTr(overrideValue ?? patient?.Phone);
    }

    private async Task<bool> HasConsentAsync(long patientId, MessageChannel channel, CancellationToken ct)
    {
        var consentType = channel switch
        {
            MessageChannel.WhatsApp => ConsentType.WhatsApp,
            MessageChannel.Email => ConsentType.Email,
            _ => ConsentType.SmsMarketing,
        };
        return await db.CommunicationConsents.AsNoTracking()
            .AnyAsync(c => c.PatientId == patientId && c.ConsentType == consentType && c.IsGranted, ct);
    }

    /// <summary>Kanal verilmediyse ilgili otomasyon kuralının politikasından çözülür; kural yoksa SMS.</summary>
    private async Task<MessageChannel> ResolveChannelAsync(string templateKey, CancellationToken ct)
    {
        var policy = await db.AutomationRules.AsNoTracking()
            .Where(r => r.TemplateKey == templateKey)
            .Select(r => (ChannelPolicy?)r.ChannelPolicy)
            .FirstOrDefaultAsync(ct);

        return policy switch
        {
            ChannelPolicy.WhatsAppFirstThenSms or ChannelPolicy.WhatsAppOnly => MessageChannel.WhatsApp,
            _ => MessageChannel.Sms,
        };
    }

    /// <summary>Şablon sırası: (kanal, dil) → (kanal, tr) → (Sms, dil) → (Sms, tr).</summary>
    private async Task<MessageTemplate?> FindTemplateAsync(
        string templateKey, MessageChannel channel, string locale, CancellationToken ct)
    {
        var candidates = await db.MessageTemplates.AsNoTracking()
            .Where(t => t.TemplateKey == templateKey && t.IsActive)
            .ToListAsync(ct);
        if (candidates.Count == 0) return null;

        return Pick(candidates, channel, locale)
            ?? Pick(candidates, channel, "tr")
            ?? Pick(candidates, MessageChannel.Sms, locale)
            ?? Pick(candidates, MessageChannel.Sms, "tr")
            ?? candidates[0];

        static MessageTemplate? Pick(List<MessageTemplate> list, MessageChannel channel, string locale) =>
            list.FirstOrDefault(t => t.Channel == channel &&
                                     string.Equals(t.Locale, locale, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Ortak yer tutucular çağıranın verdiklerinin ÜSTÜNE yazılmaz (çağıran daha spesifiktir).</summary>
    private async Task<Dictionary<string, string>> BuildParamsAsync(
        Patient? patient, IReadOnlyDictionary<string, string>? supplied, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (supplied is not null)
            foreach (var (k, v) in supplied) result[k] = v ?? "";

        if (patient is not null)
        {
            result.TryAdd(MessagePlaceholders.PatientName, patient.FullName);
            result.TryAdd(MessagePlaceholders.Balance, patient.Balance.ToString("N2"));
            var clinicName = await db.Clinics.AsNoTracking()
                .Where(c => c.Id == patient.ClinicId).Select(c => c.Name).FirstOrDefaultAsync(ct);
            if (clinicName is not null) result.TryAdd(MessagePlaceholders.ClinicName, clinicName);
        }

        return result;
    }

    private async Task<string> DefaultLocaleAsync(long tenantId, CancellationToken ct) =>
        await db.Tenants.AsNoTracking().Where(t => t.Id == tenantId)
            .Select(t => t.DefaultLocale).FirstOrDefaultAsync(ct) ?? "tr";

    // ---- Toplu hedef kitle ----

    private async Task<List<long>> ResolveAudienceAsync(BulkAudienceFilter filter, CancellationToken ct)
    {
        var patients = db.Patients.AsNoTracking();

        if (filter.HasDebt is { } hasDebt)
            patients = hasDebt ? patients.Where(p => p.Balance > 0) : patients.Where(p => p.Balance <= 0);

        if (filter.BirthMonth is { } month)
            patients = patients.Where(p => p.BirthDate != null && p.BirthDate!.Value.Month == month);

        if (filter.TagId is { } tagId)
            patients = patients.Where(p => p.Tags.Any(t => t.PatientTagId == tagId));

        // Son randevu aralığı / hekim filtresi randevu tablosundan alt sorguyla uygulanır.
        if (filter.LastVisitFrom is { } from || filter.LastVisitTo is { } to || filter.DoctorUserId is { } doctorId)
        {
            var appointments = db.Appointments.AsNoTracking()
                .Where(a => a.PatientId != null && a.Status != AppointmentStatus.Cancelled);

            if (filter.DoctorUserId is { } d) appointments = appointments.Where(a => a.DoctorUserId == d);
            if (filter.LastVisitFrom is { } f)
                appointments = appointments.Where(a => a.StartUtc >= TrTime.DayRangeUtc(f).StartUtc);
            if (filter.LastVisitTo is { } t)
                appointments = appointments.Where(a => a.StartUtc < TrTime.DayRangeUtc(t).EndUtc);

            var matched = appointments.Select(a => a.PatientId!.Value).Distinct();
            patients = patients.Where(p => matched.Contains(p.Id));
        }

        return await patients.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(ct);
    }

    // ---- Projeksiyon ----

    private IQueryable<OutboundMessageDto> Project(IQueryable<OutboundMessage> source) =>
        from m in source
        join p in db.Patients on m.PatientId equals p.Id into patients
        from p in patients.DefaultIfEmpty()
        select new OutboundMessageDto(
            m.Id, m.PatientId, p == null ? null : p.FirstName + " " + p.LastName,
            m.Channel, m.Kind, m.TemplateKey, m.RenderedBody, m.ToAddress,
            m.State, m.SkipReason, m.ProviderKey, m.ProviderMessageId,
            m.ScheduledAtUtc, m.SentAtUtc, m.DeliveredAtUtc, m.Error,
            m.AttemptCount, m.NextAttemptAtUtc, m.FallbackOfMessageId,
            m.RefType, m.RefId, m.CreditCost, m.CorrelationId, m.CreatedAtUtc);
}
