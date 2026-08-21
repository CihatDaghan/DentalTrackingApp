namespace Dental.Application.Payments;

/// <summary>
/// Sanal POS ödeme linki. Tenant bağlamı gerektirir (API ve — token'dan tenant çözüldükten
/// sonra — public/webhook akışı aynı servisi kullanır).
/// </summary>
public interface IPaymentLinkService
{
    /// <summary>
    /// PaymentIntent + sağlayıcı checkout'u oluşturur ve linki hastaya mesaj olarak kuyruğa alır
    /// (Status=LinkSent). Sağlayıcı hata verirse kayıt Failed olur.
    /// </summary>
    Task<PaymentLinkDto> CreateAsync(PaymentLinkCreateRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<PaymentLinkDto>> ListAsync(long? patientId = null, CancellationToken ct = default);

    Task<PaymentLinkDto> GetAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Sağlayıcı callback'ini işler: ÖNCE sunucudan yeniden doğrular (VerifyPaymentAsync),
    /// başarılıysa tahsilat oluşturur. İdempotenttir — aynı ProviderPaymentId ikinci kez
    /// gelirse yeni tahsilat açılmaz, <c>AlreadyProcessed=true</c> döner.
    /// </summary>
    Task<PaymentCallbackResult> HandleCallbackAsync(long intentId, CancellationToken ct = default);

    Task<PublicPaymentViewDto> GetPublicViewAsync(long intentId, CancellationToken ct = default);

    Task<PublicPaymentStatusDto> GetPublicStatusAsync(long intentId, CancellationToken ct = default);

    /// <summary>Süresi geçmiş Created/LinkSent niyetleri Expired yapar; güncellenen sayıyı döner.</summary>
    Task<int> ExpireStaleAsync(CancellationToken ct = default);
}

/// <summary>
/// Anonim (token'lı) ödeme uçları. İstekte tenant claim'i yoktur: token → tenant çözümlemesi
/// IgnoreQueryFilters ile yapılır, ardından ITenantScopeFactory ile o kiracı için scope kurulur
/// (PublicConsentService ile aynı kalıp).
/// </summary>
public interface IPublicPaymentService
{
    Task<PublicPaymentViewDto> GetByTokenAsync(Guid publicToken, CancellationToken ct = default);

    Task<PublicPaymentStatusDto> GetStatusByTokenAsync(Guid publicToken, CancellationToken ct = default);

    /// <summary>Sağlayıcı callback'i: kendi token'ımızla ya da sağlayıcı token'ıyla niyeti bulur.</summary>
    Task<PaymentCallbackResult> HandleCallbackAsync(
        Guid? publicToken, string? providerToken, CancellationToken ct = default);
}
