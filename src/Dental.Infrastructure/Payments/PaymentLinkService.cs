using Dental.Application.Abstractions;
using Dental.Application.Finance;
using Dental.Application.Messaging;
using Dental.Application.Payments;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Consents;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dental.Infrastructure.Payments;

/// <summary>
/// Sanal POS ödeme linki servisi (EDocumentDispatcher kalıbı: sürücü seçimi factory'den,
/// durum makinesi tek yerde). Hastaya giden link BİZİM public sayfamızdır; sağlayıcının
/// hosted sayfasına oradan geçilir — böylece durum yoklaması ve süre kontrolü bizde kalır.
/// </summary>
public sealed class PaymentLinkService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IIntegrationProviderFactory providerFactory,
    IMessageOutboxService outbox,
    IPaymentService payments,
    IOptions<PublicOptions> publicOptions,
    ILogger<PaymentLinkService> logger) : IPaymentLinkService
{
    public async Task<PaymentLinkDto> CreateAsync(
        PaymentLinkCreateRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0) throw new ArgumentException("Ödeme tutarı sıfırdan büyük olmalıdır.");

        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan ödeme linki üretilemez.");
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PatientId, ct)
            ?? throw new KeyNotFoundException("Hasta bulunamadı.");

        var intent = new PaymentIntent
        {
            PatientId = patient.Id,
            ClinicId = patient.ClinicId,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            Description = request.Description,
            PublicToken = Guid.NewGuid(),
            Status = PaymentIntentStatus.Created,
            ExpiresAtUtc = clock.UtcNow.AddHours(Math.Clamp(request.ExpiresInHours, 1, 720)),
            CreatedByUserId = tenant.UserId,
        };
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync(ct);

        // ConversationId = Id: sağlayıcı callback'inde eşleşme anahtarımız.
        intent.ConversationId = intent.Id.ToString();

        var resolved = await providerFactory.ResolveAsync<IPaymentProvider>(tenantId, ct);
        intent.ProviderKey = resolved.ProviderKey;

        try
        {
            var checkout = await resolved.Instance.CreateCheckoutAsync(new PaymentCheckoutRequest(
                intent.ConversationId,
                intent.Amount,
                intent.CurrencyCode,
                intent.Description ?? "Saglik hizmeti",
                patient.FullName,
                patient.Email,
                patient.Phone,
                CallbackUrl(intent.PublicToken)), ct);

            intent.ProviderToken = checkout.ProviderToken;
            intent.LinkUrl = checkout.PaymentPageUrl;
        }
        catch (Exception ex) when (ex is PaymentProviderException or HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Ödeme linki üretilemedi. IntentId={Id} Sürücü={Provider}",
                intent.Id, resolved.ProviderKey);
            intent.Status = PaymentIntentStatus.Failed;
            intent.RawResponseJson = ex.Message;
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException($"Ödeme linki üretilemedi: {ex.Message}", ex);
        }

        var message = await outbox.EnqueueAsync(new MessageEnqueueRequest(
            MessageTemplateKeys.PaymentLink,
            PatientId: patient.Id,
            Channel: request.Channel,
            Kind: MessageKind.Transactional,
            Params: new Dictionary<string, string>
            {
                [MessagePlaceholders.PaymentLink] = PublicPageUrl(intent.PublicToken),
            },
            RefType: nameof(PaymentIntent),
            RefId: intent.Id), ct);

        intent.Status = PaymentIntentStatus.LinkSent;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Ödeme linki oluşturuldu. IntentId={Id} Sürücü={Provider} MesajId={MessageId}",
            intent.Id, intent.ProviderKey, message.Id);
        return await GetAsync(intent.Id, ct);
    }

    public async Task<IReadOnlyList<PaymentLinkDto>> ListAsync(
        long? patientId = null, CancellationToken ct = default)
    {
        var source = db.PaymentIntents.AsNoTracking();
        if (patientId is { } pid) source = source.Where(i => i.PatientId == pid);
        return await Project(source.OrderByDescending(i => i.Id).Take(200)).ToListAsync(ct);
    }

    public async Task<PaymentLinkDto> GetAsync(long id, CancellationToken ct = default) =>
        await Project(db.PaymentIntents.AsNoTracking().Where(i => i.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Ödeme linki bulunamadı.");

    public async Task<PaymentCallbackResult> HandleCallbackAsync(long intentId, CancellationToken ct = default)
    {
        var intent = await db.PaymentIntents.FirstOrDefaultAsync(i => i.Id == intentId, ct)
            ?? throw new KeyNotFoundException("Ödeme linki bulunamadı.");

        // İdempotanlık 1. kapı: ödeme zaten işlenmişse hiçbir şey yapma.
        if (intent.Status == PaymentIntentStatus.Paid)
            return new PaymentCallbackResult(intent.Id, intent.PublicToken, intent.Status, intent.PaymentId, AlreadyProcessed: true);

        if (string.IsNullOrWhiteSpace(intent.ProviderToken))
            return new PaymentCallbackResult(intent.Id, intent.PublicToken, intent.Status, null, false, "Sağlayıcı token'ı yok.");

        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan callback işlenemez.");
        var resolved = await providerFactory.ResolveAsync<IPaymentProvider>(tenantId, ct);

        // Callback verisine ASLA tek başına güvenilmez: sonuç sunucudan yeniden doğrulanır.
        PaymentVerifyResult verify;
        try
        {
            verify = await resolved.Instance.VerifyPaymentAsync(intent.ProviderToken!, ct);
        }
        catch (Exception ex) when (ex is PaymentProviderException or HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Ödeme doğrulaması başarısız. IntentId={Id}", intent.Id);
            return new PaymentCallbackResult(intent.Id, intent.PublicToken, intent.Status, null, false, ex.Message);
        }

        intent.RawResponseJson = verify.RawJson;

        if (verify.Status != PaymentVerifyStatus.Success)
        {
            if (verify.Status == PaymentVerifyStatus.Failure) intent.Status = PaymentIntentStatus.Failed;
            await db.SaveChangesAsync(ct);
            return new PaymentCallbackResult(intent.Id, intent.PublicToken, intent.Status, null, false, verify.Error);
        }

        // İdempotanlık 2. kapı: aynı sağlayıcı ödeme kimliği başka bir niyette işlenmişse tekrar tahsilat açma.
        if (verify.ProviderPaymentId is { } providerPaymentId &&
            await db.PaymentIntents.AnyAsync(
                i => i.Id != intent.Id && i.ProviderPaymentId == providerPaymentId, ct))
        {
            logger.LogWarning("Mükerrer ödeme callback'i yok sayıldı. IntentId={Id} SağlayıcıÖdemeId={PaymentId}",
                intent.Id, providerPaymentId);
            return new PaymentCallbackResult(intent.Id, intent.PublicToken, intent.Status, intent.PaymentId, AlreadyProcessed: true);
        }

        var payment = await payments.CreateAsync(new PaymentCreateRequest(
            Amount: verify.PaidAmount ?? intent.Amount,
            Method: PaymentMethod.OnlineLink,
            PatientId: intent.PatientId,
            ClinicId: intent.ClinicId,
            Note: $"Online ödeme linki #{intent.Id}",
            CurrencyCode: intent.CurrencyCode), ct);

        intent.ProviderPaymentId = string.IsNullOrWhiteSpace(verify.ProviderPaymentId)
            ? null
            : verify.ProviderPaymentId;
        intent.PaidAmount = verify.PaidAmount ?? intent.Amount;
        intent.PaidAtUtc = clock.UtcNow;
        intent.PaymentId = payment.Id;
        intent.Status = PaymentIntentStatus.Paid;
        await db.SaveChangesAsync(ct);

        // Tahsilat kaydını ödeme niyetine bağla (Payment.PaymentIntentId D aşamasında hazırlanmıştı).
        await db.Payments.Where(p => p.Id == payment.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PaymentIntentId, intent.Id), ct);

        logger.LogInformation("Online ödeme tahsil edildi. IntentId={Id} PaymentId={PaymentId} Tutar={Amount}",
            intent.Id, payment.Id, intent.PaidAmount);
        return new PaymentCallbackResult(intent.Id, intent.PublicToken, intent.Status, payment.Id, AlreadyProcessed: false);
    }

    public async Task<PublicPaymentViewDto> GetPublicViewAsync(long intentId, CancellationToken ct = default)
    {
        var intent = await db.PaymentIntents.FirstOrDefaultAsync(i => i.Id == intentId, ct)
            ?? throw new KeyNotFoundException("Ödeme linki bulunamadı.");
        await ExpireIfStaleAsync(intent, ct);

        var patient = await db.Patients.AsNoTracking().FirstAsync(p => p.Id == intent.PatientId, ct);
        var clinicName = await db.Clinics.AsNoTracking()
            .Where(c => c.Id == intent.ClinicId).Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "Klinik";

        // Ödeme sayfası bağlantısı yalnız ödenebilir durumdayken verilir.
        var payUrl = intent.Status is PaymentIntentStatus.Created or PaymentIntentStatus.LinkSent
            ? intent.LinkUrl
            : null;

        return new PublicPaymentViewDto(
            clinicName, patient.FullName, intent.Amount, intent.CurrencyCode,
            intent.Description, intent.Status, payUrl, intent.ExpiresAtUtc);
    }

    public async Task<PublicPaymentStatusDto> GetPublicStatusAsync(long intentId, CancellationToken ct = default)
    {
        var intent = await db.PaymentIntents.FirstOrDefaultAsync(i => i.Id == intentId, ct)
            ?? throw new KeyNotFoundException("Ödeme linki bulunamadı.");
        await ExpireIfStaleAsync(intent, ct);
        return new PublicPaymentStatusDto(intent.Status, intent.PaidAmount, intent.PaidAtUtc);
    }

    public async Task<int> ExpireStaleAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return await db.PaymentIntents
            .Where(i => (i.Status == PaymentIntentStatus.Created || i.Status == PaymentIntentStatus.LinkSent)
                        && i.ExpiresAtUtc != null && i.ExpiresAtUtc < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, PaymentIntentStatus.Expired)
                .SetProperty(i => i.UpdatedAtUtc, now), ct);
    }

    // ---- Yardımcılar ----

    private async Task ExpireIfStaleAsync(PaymentIntent intent, CancellationToken ct)
    {
        if (intent.Status is not (PaymentIntentStatus.Created or PaymentIntentStatus.LinkSent)) return;
        if (intent.ExpiresAtUtc is not { } expires || expires >= clock.UtcNow) return;

        intent.Status = PaymentIntentStatus.Expired;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Sağlayıcının ödeme sonrası çağıracağı uç; niyeti bizim token'ımızla taşır.</summary>
    private string CallbackUrl(Guid publicToken) =>
        $"{publicOptions.Value.ApiBaseUrl.TrimEnd('/')}/api/webhooks/iyzico?intent={publicToken:D}";

    private string PublicPageUrl(Guid publicToken) =>
        $"{publicOptions.Value.BaseUrl.TrimEnd('/')}/p/payment/{publicToken:D}";

    private IQueryable<PaymentLinkDto> Project(IQueryable<PaymentIntent> source) =>
        from i in source
        join p in db.Patients on i.PatientId equals p.Id
        select new PaymentLinkDto(
            i.Id, i.PatientId, p.FirstName + " " + p.LastName, i.ClinicId,
            i.Amount, i.CurrencyCode, i.Description, i.PublicToken,
            i.ProviderKey, i.LinkUrl, i.Status, i.PaidAmount, i.ProviderPaymentId,
            i.PaymentId, i.PaidAtUtc, i.ExpiresAtUtc,
            db.OutboundMessages.Where(m => m.RefType == "PaymentIntent" && m.RefId == i.Id)
                .OrderBy(m => m.Id).Select(m => (long?)m.Id).FirstOrDefault(),
            i.CreatedAtUtc);
}
